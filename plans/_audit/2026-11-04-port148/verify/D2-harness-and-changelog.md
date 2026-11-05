# D2 verify: the 1.5.x harness, plan 020 and the commit convention for the 1.4.x port

Run 2026-11-04-port148, read-only adversarial verifier. Refs read this session:
origin/bannerlord-1.4.5 = 1b00d1acd (2026-10-30), origin/bannerlord-1.5.x = e45f6df2c (2026-11-04),
fork a4c6d7c30, local HEAD c5b84fb41. Every 1.4.x hook path below is `origin/bannerlord-1.4.5:<path>`
unless marked otherwise; the hooks, CLAUDE.md, lint_docs.py and validate_moduledata.py are identical
between c5b84fb41 and 1b00d1acd (none is in SP\diff_localHEAD_vs_origin145.txt).

## 0. Bottom line

1. The executor-safety subset the batch notes propose (7b342ed59, d01bddf10, 785b2703e, the ModuleId
   change of 1d1abd694, 143f0fa85, 4b7e36250) is necessary but not sufficient, and one member is harmful
   during the port campaign.
   - Missing, and more important than anything in the subset: on 1.4.x nine PreToolUse gates print a
     top-level `{"permissionDecision": ...}`, which Claude Code does not read. The v2.0.28 subject gate,
     the CHANGELOG gate, the files-tracked, ModuleData and doc-drift gates, both confirm gates and the
     /freeze boundary are therefore inert from every directory today. 1.5.x fixed this inside c79a58521
     (subject "Add scripts for generating and validating animal animation clips", track diagnostics),
     which the draft map routes to WP108, not WP102.
   - Also missing: `export PYTHONIOENCODING=utf-8` (a command holding a character outside cp1252 empties
     the Python-parsed gates, reproduced here), a CLAUDE.md trim (the doc-drift gate fails today and will
     deny every feature-doc commit the moment it works), a Serena edit-tool deny list, and
     bannerlord-1.5.x in validate-push's protected list.
   - Harmful during the campaign: 4b7e36250's `docs/reviews/lessons/*.md merge=union`. On a cross-line
     pick the union driver keeps the 1.5.x side of the whole conflict region, so it would silently import
     every 1.5.x lesson in that region, not only the commit's own.
2. No hook on 1.4.x judges a `git cherry-pick`. Executors must never let cherry-pick create the commit:
   apply with `git cherry-pick -n`, then commit with `git commit -F <file>` in a separate, sequential call.
3. Plan 020: adopt it on 1.4.x, before any executor starts and before the gate output contract is fixed,
   so the CHANGELOG gate is deleted rather than switched on. It needs one maintainer decision first: the
   next 1.4.x release version (v2.0.29 to v2.0.36 are 1.5.x tags; the generator accepts only vX.Y.Z).
4. The full 1.5.x harness transplant (WP134) is a separate, later, decision-heavy package, not an executor
   precondition.
5. Provenance: `-x` writes nothing on a conflicted pick and nothing with `-n`; 612 of the 655 port-ish
   commits conflict. Use a hand-written `Ported-from: <40-hex sha>` trailer on every port commit.

## 1. Claim verdicts

| # | Claim (source) | Verdict | Evidence |
|---|---|---|---|
| 1 | Relative registrations, so a persistent cd silences the v2.0.28 subject gate and the CHANGELOG gates; 7b342ed59 fixed it on 1.5.x (brief2) | PARTIAL | All 28 registrations are relative (settings.json:17-232, 0 use CLAUDE_PROJECT_DIR). Hooks docs: "Handlers run in the current directory", and a script that cannot start (127) is a non-blocking error. But those two gates are inert from any directory (section 2.2 A), and the CHANGELOG Stop reminder prints to stderr with exit 0, which "Claude never sees". The cd problem bites the three exit-2 gates (validate-push, block-no-verify, config-protection) |
| 2 | Port first: 7b342ed59 (+d01bddf10), 785b2703e, the ModuleId hunk of 1d1abd694 (b10 note 1) | PARTIAL | Needed, but misses the output contract, PYTHONIOENCODING and the CLAUDE.md budget. 1d1abd694 has no separable ModuleId hunk (it rewrites 7 files, 1,049+/437-, and deletes closing-the-loop.md) |
| 3 | PowerShell tool calls bypass every git gate (b9 note 4, b10 note 2) | CONFIRMED | settings.json:30 `"matcher": "Bash"`; hooks docs: "`Bash` matches only the Bash tool". This session offers PowerShell as its primary shell |
| 4 | 143f0fa85 and 7b342ed59 apply without plan 027; laptop bash is 5.3.9 (b10 note 3) | CONFIRMED with adaptations | `bash --version` here: 5.3.9. 11 here-string sites in 10 hooks (section 3.5). The hang itself was not reproduced here: UNVERIFIED on this laptop |
| 5 | The hard-blocking check-changelog-changed.sh would stop executors (b10 note 5) | PARTIAL | Not blocking today (top-level JSON, :123-125). Once fixed it fires only when the commit holds `.claude/*`, CLAUDE.md or AGENTS.md (:97-104): 246 of the 655 port-ish commits, not every port |
| 6 | The 1.4.x hooks would make every cherry-pick hand-edit CHANGELOG (RELEASE-INTEGRITY-09) | REFUTED | No commit gate matches `git cherry-pick` (e.g. check-changelog-changed.sh:36-42), the gate is inert, and its scope is note 5's |
| 7 | Pre-2026-09-25 1.5.x commits all touch CHANGELOG.md (RELEASE-INTEGRITY-09) | REFUTED | classified.jsonl: 188 of 277 commits dated up to 09-25; 135 of 194 port-ish ones. After 09-25 only the 4 release commits touch it |
| 8 | Land 4b7e36250 before the bulk picks; cherry-pick reads the target's .gitattributes, so lessons conflicts resolve automatically (4b7e36250 port_notes) | Mechanism CONFIRMED, advice REFUTED | gitattributes.html:536-549 (worktree attributes) and :1783-1785 (merge attribute applies to cherry-pick). Union "take[s] lines from both versions" (:1846-1848). For 143f0fa85's lesson: base->ours `@@ -2184,710 +2111,3 @@`, base->theirs `@@ -2891,3 +2891,25 @@`; the regions adjoin, so one conflict region, and union would emit about 710 lines of other 1.5.x lessons. Inferred from the documented semantics and the hunk geometry, not executed |
| 9 | 785b2703e picks clean | CONFIRMED, one semantic fix | review-codex SKILL.md is identical at 785b2703e^ and 1b00d1acd (diff stat lists only verify/SKILL.md). The new probe string "Evidence, never invention" has 0 hits in 1.4.x AGENTS.md; use "Verify evidence before making claims" (AGENTS.md:27) |
| 10 | 7j ports with the trunk changed to bannerlord-1.4.5; 1.4.x frontmatter already anchored (7b342ed59 notes) | CONFIRMED | validate-push.sh:113-118 protects master, main, bannerlord-1.4.5. The 5 frontmatter commands use `bash ${CLAUDE_PROJECT_DIR}/...`, which d01bddf10's regex accepts |
| 11 | 1.4.x CHANGELOG practice lapsed; the generator takes only vX.Y.Z; v2.0.29+ are 1.5.x tags (b9 note 3) | CONFIRMED | `git log a4c6d7c30..origin/bannerlord-1.4.5 -- CHANGELOG.md`: only c5b84fb41, ed3f42792 (09-15). ccef172ad touched `.claude/` with no CHANGELOG. changelog_from_commits.py:34-35. Tags v2.0.29 (a12fec9b8) to v2.0.36 (aa708f620) are not ancestors of 1b00d1acd; `git describe` there gives v2.0.28 (8ce42fe85), 118 non-merge commits back |
| 12 | Whether cherry-pick runs commit-msg is unverified (0382d5d2a notes) | UNVERIFIABLE here | githooks.html:642-643: commit-msg "is invoked by git-commit(1) and git-merge(1)"; cherry-pick is not listed. No execution allowed in this run. Design so nothing depends on it |
| 13 | WP102 covers executor safety (wp_map.json) | REFUTED as drafted | WP102 groups: issue-681, issue-690, harness-non-deploying-verify, git-multisession-safety, docs-lessons-union-merge. c79a58521 (output contract) is group crash-report-rethrow-stack -> WP108, wave 1 |

## 2. What the 1.4.x hooks enforce (question 1)

### 2.1 Registrations

`.claude/settings.json` (1.4.x): 28 registrations across 9 events, all named by a relative path. The 10
git gates sit in one PreToolUse group with matcher `Bash` (:28-86); config-protection is on `Edit|Write`
(:87-96). There is no `permissions` block. The tracked `.claude/settings.local.json` denies 9 git and
filesystem MCP write tools but none of Serena's. No `.githooks/` exists on the ref, `core.hooksPath` is
unset on this laptop and `.git/hooks` holds only samples, so no git hook runs here at all.

### 2.2 Cross-cutting defects (each verified this session)

- **A. Output contract.** Hooks docs ("PreToolUse decision control", fetched 2026-11-04) put the
  decision in `hookSpecificOutput.permissionDecision`; the deprecated top-level fields are `decision`
  and `reason`. A top-level `permissionDecision` is not a documented field, and 1.5.x proved it ignored
  live: "a live commit the label gate had denied ran anyway" (1.5.x docs/reference/rule-provenance.md:143,
  #647). On 1.4.x nine scripts print exactly that form: block-broad-git-add.sh:145,
  block-dangerous-git.sh:113, check-changelog-changed.sh:124, check-claude-files-tracked.sh:87,
  check-commit-subject-version.sh:73, check-doc-config-drift.sh:97,119,
  check-moduledata-validation.sh:112,130, check-native-dll-crt.sh:92,120,
  .claude/skills/freeze/check-freeze.sh:155. The rule that documents the contract is wrong too
  (harness-facts.md:33). The tests stay green because section 6 greps the text
  `"permissionDecision":"deny"`, which matches both forms (tools/test_hooks.sh, section 6). The only
  gates that work are exit-2 ones: block-no-verify.sh:52-55, validate-push.sh:121-124 (force push to a
  protected branch only), config-protection.sh:60-74, mcp-health-check.sh:42.
- **B. cp1252 stdout.** The hooks extract the command with
  `print(d.get("tool_input", {}).get("command", ""))` inside `try/except: pass`. Run here with a payload
  holding U+2192, the same snippet raised UnicodeEncodeError and printed nothing (Python's piped stdout
  is cp1252 on this laptop; PYTHONIOENCODING is unset; 1.4.x _pybin.sh and settings.json do not set it).
  An empty command means allow. This hits the Python-only gates (subject, CHANGELOG, files-tracked,
  ModuleData, native DLL, doc drift) on every machine. The jq-first gates (validate-push,
  block-no-verify, the two ask gates, config-protection) take jq on this laptop, which has jq from WinGet;
  the hook comments say the desktop has no jq (validate-push.sh:22-26), so there every gate is exposed.
  1.5.x fixed it in c79a58521 (_pybin.sh `export PYTHONIOENCODING=utf-8`).
- **C. Relative paths** (claim 1). After a persistent `cd` in a main session, every hook gets rc 127,
  which is non-blocking. Subagent Bash calls reset their cwd, so this mainly hits main sessions.
- **D. PowerShell** (claim 3).
- **E. Wrong repository for worktree executors.** Every commit gate runs
  `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"` (for example check-commit-subject-version.sh:53,
  check-changelog-changed.sh:44) and then reads that tree's index and SubModule.xml. Hooks docs:
  CLAUDE_PROJECT_DIR "stays put" at the session's start directory. So a commit made in a worktree is
  judged on the main checkout's index. Plan 020 records the same effect on 1.5.x (plan file lines 404-409).
- **F. Same-command staging.** A PreToolUse gate runs before the whole command, so in
  `git add X && git commit ...` it sees the index without X. Stage in one call and commit in the next,
  sequentially, never as parallel sibling calls (a deny stops only its own call: 1.5.x harness-facts.md:67).
- **G. Invisible reminders.** Hooks docs: "Claude never sees stderr from a hook that exits 0." All four
  Stop hooks (check-changelog-updated.sh:35, check-deep-review.sh:28, check-verification-evidence.sh:46,
  check-version-tagged.sh:49) and validate-push's protected-branch warning (:127-129) are invisible.
  session-start.sh prints to stdout, which SessionStart does show.

### 2.3 Per hook: trigger, effect today, effect once A to E are fixed, how an executor satisfies it

| Hook (1.4.x) | Fires on | Today | When working | Executor satisfies it by |
|---|---|---|---|---|
| block-no-verify.sh | any command with token `git` and text `--no-verify` | blocks (exit 2) | same | never writing `--no-verify`, even inside a message (put messages in files) |
| validate-push.sh | a `push` token after a `git` token | blocks force push to master, main or bannerlord-1.4.5; bannerlord-1.5.x is not protected (:113-118) | same | never pushing; integration and push are the maintainer's |
| check-commit-subject-version.sh | `git commit` with `-m`, `-F` or a heredoc | inert | denies unless the subject matches `^[a-z][a-z0-9]*(?:\([^)]+\))?!?: (vX.Y.Z[.W]) - \S` with the version equal to SubModule.xml (:134-161). No length check. Lets `-C/-c/--reuse-message`, `--fixup/--squash`, `--amend --no-edit` and editor commits through unchecked (:76-80, :129-132) | `git commit -F <file>` with a v2.0.28 subject; never `-C`, `-c` or `--no-edit` on a port |
| check-changelog-changed.sh | `git commit` whose staged set (plus amend and pathspec files) holds `.claude/*`, CLAUDE.md or AGENTS.md | inert | denies without CHANGELOG.md in the same set | deleted by plan 020; if 020 is declined, stage a CHANGELOG entry or keep those paths out of code ports |
| check-claude-files-tracked.sh | every `git commit` | inert | denies if any md/sh/json/yaml under `.claude/{skills,agents,rules,hooks}` in the MAIN checkout is untracked and unstaged, or ignored (:55-71). Clean today | no stray files there; a deny naming files you did not create is a STOP |
| check-moduledata-validation.sh | `git commit` staging `Main/_Module/ModuleData/*.xml` | inert | denies on validator rc 1 (19 codes, :97-106) | on this laptop the same 19 codes report 3,423 BROKEN_ITEM_REF today (rc 1; `ladder_*` 559 hits, `starter_*` families; defined neither in the repo nor in the installed Armory ModuleData). Every ModuleData commit would be denied on this laptop. Environment gap or public defect: UNVERIFIED, owned by the xml-data lens. STOP, or run data ports on the desktop |
| check-doc-config-drift.sh | `git commit` touching docs/features/*.md, ModuleData/*.json, CLAUDE.md, pinned version or the API snapshot | inert | denies when `lint_docs.py --fail-on-drift` exits 1 | run here: exit 1, CLAUDE.md is 46,704 B on disk (46,283 B as a blob, 421 CRs) against the 46,000 B cap (lint_docs.py:49, gating at :1268-1275). Every feature-doc commit would be denied until CLAUDE.md is trimmed |
| check-native-dll-crt.sh | the vendored NativeSkinFixes DLL staged | inert | denies on a dynamic CRT | n/a (feature parked) |
| block-dangerous-git.sh | reset --hard, clean -f, branch -D, stash drop/clear, checkout with `--`, `-f` or a dot path, restore other than `--staged` alone (:82-102) | inert (also emits invalid JSON for a multi-line command) | ask; an ask prompts even in bypass mode and an unattended run waits (1.5.x harness-facts.md:64, EMPIRICAL) | never using those forms; resolve with `git checkout --ours <path>` (no `--`), abort with `git cherry-pick --abort` |
| block-broad-git-add.sh | add -A/--all/-u/--update/., commit -a/--all (:98-117) | inert | ask, same stall | explicit `git add <paths>` |
| config-protection.sh (Edit/Write) | basename settings.json, settings.local.json, Directory.Build.props; docs/adrs/*.md | blocks (exit 2) | same | maintainer edits. 17 port-ish commits touch .claude/settings.json, 4 Directory.Build.props, and many touch ADRs (250e1b321 touches every ADR). A git apply or cherry-pick bypasses this hook; plans must route those files to the maintainer, not exploit the gap |
| /freeze check-freeze.sh | Edit/Write outside the boundary | inert | deny | nothing today; do not rely on /freeze or /investigate's lock until fixed |

### 2.4 What fires on a cherry-pick

For `git cherry-pick ...` the commit gates parse the command, find no `git commit` and return `{}`.
block-no-verify and validate-push act only if their text appears. The PostToolUse markers run but gate
nothing. So the commit a cherry-pick creates is judged by nothing in the Claude harness, and by no git
hook on this laptop. In this tool environment `GIT_EDITOR=true`, so `git cherry-pick --continue` also
commits the default (1.5.x) message unedited.

## 3. The executor-safety subset (question 2)

Classifier pick simulation (SP\agg\classified.jsonl) plus hunk checks this session.

### 3.1 7b342ed59 (anchor registrations on CLAUDE_PROJECT_DIR)

Applies in substance, not as a pick: its settings.json pre-image already had the `Bash|PowerShell`
group, no CHANGELOG hooks, the graphify gate and a permissions block (diff 1b00d1acd vs 7b342ed59^).
On 1.4.x: prefix all 28 commands with `"$CLAUDE_PROJECT_DIR"/` (a settings.json edit, so the
maintainer's, because config-protection exits 2). Port test_hooks.sh 7j as written, with the payload
target changed from bannerlord-1.5.x to bannerlord-1.4.5; the 1.4.x file has the helpers it needs
(REPO :26, ok/bad/head2 :34-36, HPY :43). hook-authoring.md differs from its pre-image (92 changed
lines), so the rule row is hand-written; the lesson is appended by hand.

### 3.2 d01bddf10

Take only the 7j frontmatter extension (its regex accepts the 5 existing `${CLAUDE_PROJECT_DIR}`
commands) and the block-no-verify.sh comment hunk (1.4.x :14-15 match its pre-image). The _pushjudge.py,
validate-push.sh, test_shellwords.py and catalog hunks target code 1.4.x does not have.

### 3.3 785b2703e (non-deploying /verify and /review-codex)

Clean pick. It changes verify SKILL.md:19 and :27 to `-p:DisableModuleCopy=true -p:ModuleId=` and drops
`--no-build`; review-codex SKILL.md:239-240 likewise; it moves the ID cheatsheet and lessons blocks into a
new references/prompt-fixed.md; it repoints Phase 3h from AGENTS.md to .ai/review-reference.md:42 (which
exists on 1.4.x with the "What Codex does especially well" :44 and "Look harder here" :141 headings
the new text names; codex-track-record.md and agents-md-review-lessons-archive.md also exist); and it
replaces the AGENTS.md-over-32-KB note (1.4.x AGENTS.md is 3,690 B; reviewctl.py:367 caps it at 8,192).
One adaptation: its probe string is absent from 1.4.x AGENTS.md (claim 9).

### 3.4 1d1abd694, the ModuleId change

Not a hunk. The 1.4.x equivalent is a hand edit, and it should be a sweep, because the gap is wider than
the improve skill. Lines on the 1.4.x ref with `dotnet build|test` and no `ModuleId=`:
improve SKILL.md:22,38; audit-playbook.md:89; closing-the-loop.md:29; plan-template.md:79,80,131,137,138;
verify SKILL.md:19,27; review-codex SKILL.md:239,240; build-fix SKILL.md:14,33,113; deslop SKILL.md:16,67;
localize SKILL.md:33; new-culture SKILL.md:31; verify-bindings SKILL.md:32; agents/refactoring-specialist.md:52,64,87;
rules/evidence-over-claims.md:26,40; docs/ai-includes/agent-operating-manual.md:41-43;
agent-teams.md:27,28,57,78,86,228,229,297; new-culture-authoring.md:322,323,378; testing-guide.md:659-674,705;
tdd-enforcement.md:355. Main/TAOM.csproj:133-136 states DisableModuleCopy does not gate CopyModule, so each
of these deploys into `C:\Program Files (x86)\...` on this laptop. The 1.5.x gate for this is
`check_unflagged_dotnet` in lint_docs.py (35ff005dc; :1288-1306 at the tip, dirs :1215-1216, regex
:1217-1218); lift that function with `commit=None` as the RED test.

### 3.5 143f0fa85 (no here-strings)

Applies. Sites on 1.4.x: block-broad-git-add.sh:118, block-dangerous-git.sh:103,
check-changelog-changed.sh:104,115 (a 1.4.x-only hook the commit never touched), check-doc-config-drift.sh:78,
check-moduledata-validation.sh:73, check-native-dll-crt.sh:68, check-verification-evidence.sh:42,
detect-docs-gaps.sh:43, mark-verification-run.sh:74, validate-push.sh:48. The simulation picked 7 of
these scripts cleanly; mark-verification-run.sh and validate-push.sh conflict. Semantic trap at
validate-push.sh:48: `read -r -a TOKENS <<< "$CLEAN"` reads only the first line, while 143f0fa85's
`set -f; TOKENS=($CLEAN); set +f` splits every line (1.5.x judges one line at a time, so there CLEAN has
no newline). Either keep the first-line semantics (`${CLEAN%%$'\n'*}`) or adopt the multi-line split on
purpose, with test rows for a force push on line 2. Test 4e ports nearly verbatim (it needs HPY, SANDBOX,
head2, ok, bad, all present). Test 7h needs rewriting: 1.4.x has no `decision_of`, no PowerShell
registrations and protects bannerlord-1.4.5.

### 3.6 4b7e36250 (union merge for lessons)

The .gitattributes line appends cleanly (the conflict is the 1.5.x-only Shaders/Sources context). Do not
land it before or during cross-line picks into lessons files (claim 8). It is safe and useful once picks
no longer touch docs/reviews/lessons, for merging 1.4.x work-package branches that each append. The
docs/modding/clans.md half is a separate docs item.

### 3.7 What WP102 must add

1. The output contract from c79a58521 (harness half only): hookSpecificOutput in the 8 hooks and
   check-freeze.sh, JSON-encoded reasons in the two ask gates, PYTHONIOENCODING in _pybin.sh,
   harness-facts.md:33 corrected, test_hooks.sh 5c (no top-level emitter) and a multi-line ask-validity
   row, section 6 switched to parse the nested decision.
2. CLAUDE.md under 46,000 B on disk (CRLF adds 421 B here): target 45,500 B or less as a blob.
3. Serena: in this session Serena offers replace_content, replace_in_files, replace_symbol_body,
   insert_after_symbol, insert_before_symbol, rename_symbol, safe_delete_symbol and the memory writers,
   and no Edit/Write/Bash hook sees them (1.5.x 7d13adc36, "Decision 58"). Deny them (7d13adc36's list)
   or remove Serena (9cfcadd07): maintainer decision.
4. `bannerlord-1.5.x` in validate-push.sh is_protected (:113-118), plus a 7j-style row.
5. Optional: .githooks/commit-msg with tools/check_commit_msg.py (0382d5d2a). Its SUBJECT regex is the
   1.4.x hook's pattern; it adds the 72-character limit and the AI-attribution refusal; repoint its
   docstring from AGENTS.md "Git and commits" (absent on 1.4.x) to CLAUDE.md "Commits"; add
   `.githooks/* text eol=lf`; the maintainer runs `git config core.hooksPath .githooks` once. Git hooks
   run in the committing worktree, so they also fix defect E for the subject rule.

## 4. Plan 020 on 1.4.x (question 3)

**What it removes** (1.5.x plans/020-changelog-at-release.md:124-131, :212-231, :332-353):
check-changelog-changed.sh and check-changelog-updated.sh with their registrations (1.4.x settings.json
:42-46 and :203-207, maintainer's edit), the "CHANGELOG every session" duty in about 40 lines (44 files
on 1.4.x mention CHANGELOG; all plan-020 scope files exist on 1.4.x except
deep-review/lenses/4-completeness.md and armory-audit/SKILL.md), and the session-start CHANGELOG block.
It archives CHANGELOG.md (1.4.x: 1,472,650 B, 19,381 lines, newest `## 2026-09-15`, starts with a doubled
BOM EF BB BF EF BB BF) to docs/changelog-archive/ and writes a short generated header. Nothing in
TAOM.Tests, build.ps1, *.ps1, *.props or *.csproj parses CHANGELOG.md (only comments cite dates).

**What /release then needs:** tools/changelog_from_commits.py and its tests at the 1.5.x tip blob
(ae85381dc, 6e4bfedb7, 971d8e975, 5b242cee6, plus 9bfd0f411's dedupe). Range: `--since` defaults to
`git describe --tags --abbrev=0 --match 'v[0-9]*'` (:159-161, :177-179), which is v2.0.28 on 1.4.x, so
the first section covers 118 commits plus every port; unlabelled ones go to their own group. Version:
VERSION_ARG_RE `^v\d+\.\d+\.\d+$` and RELEASE_HEADING_RE `^## v\d+\.\d+\.\d+ \(` (:34-35). v2.0.29 to
v2.0.36 already exist as 1.5.x tags, so the 1.4.x scheme is a maintainer decision; a 4-part v2.0.28.N
passes the subject hook (`(?:\.\d+)?`) but needs both generator regexes widened, or the next `--write`
treats the 4-part heading as hand-written and refuses. The parity test that reads the subject gate needs
adapting (6e4bfedb7 notes). /release Phase 5 and release-process.md:76 change to "run the generator".

**What breaks mid-stream:** a branch holding a CHANGELOG.md edit meets the archive move as a modify or
rename conflict; a hand-written `## ` section above the releases makes insert_section refuse (exit 2) at
the next /release; plans written under one rule mislead executors under the other; and if the output
contract is fixed first, the CHANGELOG gate starts denying 246 port-ish commits. Hence: decide before
plans are written, land before wave 1, and land before section 3.7 item 1.

**Recommendation: adopt.** With the gates working, the 1.4.x rule costs a CHANGELOG hunk in every
commit touching .claude/, CLAUDE.md or AGENTS.md (246 of 655 port-ish), each a conflict point between
parallel executors. Under 020 the 135 CHANGELOG-touching 1.5.x commits (all dated 2026-09-14 to 09-25)
resolve by always keeping 1.4.x's file. The later transplant brings 1.5.x files that already assume 020.
The cost is two decisions (adopt; version scheme) and one discipline: bodies must be written for 1.4.x
readers, not pasted from 1.5.x.

**If declined, the minimal convention:** code ports never touch CHANGELOG.md (drop the 1.5.x hunk) and
carry no .claude/, CLAUDE.md or AGENTS.md hunks (those go to the single harness owner, WP134), so the
gate never fires on them. The integrator writes one entry per merged work package, serially on the
trunk; executors never edit CHANGELOG.md. WP102's own commits each stage an entry (one executor, serial).
Do not put `merge=union` on CHANGELOG.md: on cross-line picks it would import the 1.5.x entries in the
conflict region, the same mechanism as claim 8.

## 5. Harness-sync design (question 4)

Scale (diff 1b00d1acd vs e45f6df2c): .claude, .ai, .agents, .codex, .githooks, .serena, AGENTS.md and
CLAUDE.md differ in 171 files (+9,225/-8,797). tools/ differs in 638 files (480 only on 1.5.x, 150
modified), so a whole-tools transplant is wrong: take only harness tools. 1.5.x has 22 skills against 43
(it removed 27, including build-fix, research, taom-src, commit-split, context-save/restore, humanizer,
new-culture, lord-skills, verify-bindings, xslt-check), replaced 3 agents with 4, dropped .serena (6
files) and four MCP servers, untracked settings.local.json (470ee64e9), disabled all 8 plugins and cut
CLAUDE.md from 46,283 B to 8,156 B. Each of those is a maintainer decision, not a mechanical port.

**Stages.** (1) WP102: hand-port the safety fixes onto the 1.4.x hooks (sections 3.1 to 3.7); small and
testable with the 509-line test_hooks.sh plus new rows. (2) WP103: plan 020. (3) WP134 at a wave
boundary, after the decisions: transplant from a pinned 1.5.x commit (`git checkout <sha> --
<harness paths>` in a dedicated worktree, explicit `git rm` for files 1.5.x removed), then adapt.
WP134 supersedes the stage-1 hand edits; that duplication is the price of having safe executors first.

**Keep from 1.4.x:** `.claude/pinned-game-version.txt` v1.4.8 (1.5.x: v1.5.6). Replace 1.5.x engine facts
in the 10 files naming v1.5.x (AGENTS.md, .ai/review-reference.md, rules adapters, gui-ui,
harmony-patches, troops, xslt, skills engine-bump, native-crash-triage, the pin) with 1.4.8 facts; never
carry a 1.5.x claim such as MBGlobals.GetActionSet returning a default (it throws in v1.4.8, brief2).
Subject rule: the 1.5.x end-state check-commit-subject-version.sh reads the version from the commit's
own repository (taom_git_repo_dir), so it yields v2.0.28 on 1.4.x unchanged; keep the 1.4.x CLAUDE.md
version rule text. The CHANGELOG duty and both hooks only if 020 is declined (then rewrite
check-changelog-changed in the end-state conventions). Serena per decision. validate-push already
protects both trunks at the tip (validate-push.sh:36).

**Drop or adapt:** Client/ (13 files), TAOM.Client (5), Fords of War (6), Long Defeat (1: game-run),
ShaderCacheKeeper (1: release skill) unless WP110 ports it, graphify (17 files, a gate and a mandatory
agent step; graphify is not installed here), upstream-watch (needs tools/upstream_watch.py, gh and the
network), the deep-review lenses' Client/ and Fords of War lines, .githooks/pre-commit's validator codes
absent on 1.4.x (SETTLEMENT_ASSIGNMENT_DRIFT, SKILL_TEMPLATE_MISMATCH, MISSING_COLLISION_BODY,
COLLISION_BODY_BORROWED, ITEM_SLOT_REJECTED, SCHEMA_INVALID, ARMOUR_ACQUISITION_REF,
CREATURE_GEAR_OBTAINABLE, BROKEN_FACTION_REF, TROOP_ID_REMOVED) and its `generate_career_kits.py --verify`,
and desktop `E:\` paths (24 files) made per-machine. The tip's hooks and test_hooks.sh (3,916 lines) need
tools 1.4.x lacks: check_commit_msg.py, generate_career_kits.py, graphify_taom.py, native_archive.py,
upstream_watch.py, worktree_audit.py, context_budget_scan.sh, audit_armory_refs.py and six test files.
Port the harness ones; delete the rows for the rest.

**Verification:** `bash tools/test_hooks.sh` (0 failed), `python -m unittest discover -s tools/tests -t .`
(no failure the pre-transplant baseline lacks), `python tools/lint_docs.py --fail-on-drift` (exit 0),
`python tools/reviewctl.py lint` (exit 0), `python tools/audit_claude_config.py --min HIGH` (no new HIGH),
then /security-scan; grep gates for the drop tokens; live proofs in a fresh session (below).

## 6. Commit convention for ported commits (question 5)

**What 1.4.x accepts:** only check-commit-subject-version.sh judges messages (pattern in 2.3), and only
for `git commit`. No commit-msg hook exists. The 72-character limit and "no AI attribution" are CLAUDE.md
rules nothing enforces. 1.5.x labels v2.0.29 to v2.0.36 match the pattern but fail the version
comparison. Measured on the 655 port-ish subjects: 35 carry no label (e.g. c79a58521, 30abf550b,
719074072) and need new subjects; 14 exceed 72 characters after relabelling (e.g. 197454e68 at 83,
f54581d6c at 80).

**Provenance:** git-cherry-pick.html:535-541: `-x` appends its line "only for cherry picks without
conflicts"; with `-n` nothing is committed. 612 of the 655 conflict. Precedent on 1.4.x: f8c9c8ecb:21,
d8e3777cd:55 and 8a6316c09:50 carry a hand-written `(cherry picked from commit <8-hex> on
bannerlord-1.5.x)`. Recommend a `Ported-from: <40-hex>` trailer, one per included source commit, in the
final paragraph with the other trailers. A trailer must be single-line or indented on continuation (git
did not parse 1d1abd694's unindented `Not-tested:` paragraph as a trailer). It is extractable
(`%(trailers:key=Ported-from,valueonly)`), survives conflicts and squashes, and keeps the original
identity a relabelled subject loses. Ledger queries must accept both forms.

**Template** (write it to a file outside the repo, then `git commit -F <file>`):

```
<type>(<scope>): v2.0.28 - <what this commit does on 1.4.x, 72 chars max>

<What changed on 1.4.x and why, wrapped at 72. For a partial port, what
was left out and why. Adaptations for v1.4.8 with the dump path or
taom-src type that proves them. No em or en dashes.>

Research: <v1.4.8 dump path or type, when engine-facing>
Not-tested: <what needs a live game, when it applies>
Ported-from: <40-hex sha of the 1.5.x commit>
```

Keep the 1.5.x type and scope (the generator groups by type; a "port" type would merge every port into
one group). No Co-Authored-By or "Generated with" line: CLAUDE.md "Commits" says no AI attribution, and
it outranks the session's attribution reminder.

## 7. Skills to recommend

/security-scan after any hook or settings change (CLAUDE.md routing); /verify in its non-deploying form
once 785b2703e lands; /scope-check when a port tempts harness edits inside a code work package; /release
only after the version-scheme decision; /investigate if a fixed gate denies something unexpected.

## 8. Commands run (all read-only)

git show, log, diff, ls-tree, grep, rev-parse, merge-base, describe, config --get and worktree list on the
refs above; `python tools/lint_docs.py --fail-on-drift` (exit 1, CLAUDE.md size); `python
tools/validate_moduledata.py` with the hook's 19 codes (exit 1, 3,423 errors) and with BROKEN_ITEM_REF
alone; the hooks' extraction snippet on a U+2192 payload (UnicodeEncodeError); Python reads of
SP\agg\classified.jsonl and wp_map.json; local git 2.54 docs (git-cherry-pick, githooks, gitattributes);
WebFetch of code.claude.com/docs/en/hooks and /settings. One stray stderr file this run wrote under /tmp
was deleted at once. No build, test, cherry-pick, merge-tree or hook-suite run.
