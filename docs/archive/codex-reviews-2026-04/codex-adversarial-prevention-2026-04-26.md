NEEDS FIXES + 1 HIGH / 3 MEDIUM / 2 LOW findings

## A. Known Suspects Response

1. DISPUTED — `check-changelog-changed.sh`'s JSON output is safe as written. The only deny payload is a static heredoc at `.claude/hooks/check-changelog-changed.sh:77-78`, and if stdin JSON parsing fails the script just leaves `COMMAND` empty at `.claude/hooks/check-changelog-changed.sh:19-26` and returns `{}` through the non-match path at `.claude/hooks/check-changelog-changed.sh:29-31`. I do not see a malformed-JSON path from the current fixed message.

2. CONFIRMED — the decision logic misses at least one valid commit form. Both hooks gate on `*"git commit"*` at `.claude/hooks/check-changelog-changed.sh:29-31` and `.claude/hooks/check-claude-files-tracked.sh:28-30`, so `git commit -F msg.txt` and `git commit <<EOF` still match, and a commit message containing the words `git commit` does not create a second execution path because the `case` runs once. But `git -C path commit` does not contain the contiguous substring `git commit`, so both hooks silently skip that valid commit form. `git diff --cached --name-only` at `.claude/hooks/check-changelog-changed.sh:43` is index-based, so a partially staged `CHANGELOG.md` still appears in the staged set.

3. DISPUTED — I do not see the claimed false positives in `check-claude-files-tracked.sh`. The recursive `find "$dir" -type f ...` at `.claude/hooks/check-claude-files-tracked.sh:43-58` does descend into nested subdirectories, so `.claude/skills/freeze/check-freeze.sh`-style paths are covered. The file-extension filter at `.claude/hooks/check-claude-files-tracked.sh:58` excludes `.gitignore`, so `.claude/tmp/freeze/.gitignore` is not inspected. A file already removed by `git rm` is absent from the on-disk traversal, so it is not falsely flagged. A tracked file deleted on disk is also absent from traversal; that is consistent with the script's stated contract of checking files that "exist on disk" at `.claude/hooks/check-claude-files-tracked.sh:40-41`.

4. CONFIRMED — `audit-review-counter.sh` is regex-brittle. It hard-requires the exact REVIEW-LOG wording at `tools/audit-review-counter.sh:25-29` and the exact AGENTS header shape at `tools/audit-review-counter.sh:36-39`, so punctuation or wording drift will fail the validator even if the numbers are correct. I do not confirm the quoted-string injection concern in `--fix`: the script passes `OLD_HEADER` and `NEW_HEADER` as separate argv values into Python at `tools/audit-review-counter.sh:67-77`, so embedded apostrophes or backticks in the header text do not get re-evaluated by the shell.

5. UNVERIFIED — most of `harness-facts.md` matches current docs, but not all of the wording is fully verified as written.
   Source links:
   `https://code.claude.com/docs/en/skills`
   `https://code.claude.com/docs/en/hooks`
   `https://code.claude.com/docs/en/memory`
   - Skill bodies load lazily when used: confirmed by the skills docs at `https://code.claude.com/docs/en/skills` lines 86-88 and 308-310.
   - Skill frontmatter fields are documented in the table at `https://code.claude.com/docs/en/skills` lines 227-241; `triggers:` does not appear there, so the "undocumented" claim is reasonable by omission.
   - Skill/agent frontmatter hooks are active only while the component is active: confirmed by the hooks docs at `https://code.claude.com/docs/en/hooks` lines 304-310 and 549-550.
   - Rules without `paths:` load at launch and rules with `paths:` are conditional: confirmed by the memory docs at `https://code.claude.com/docs/en/memory` lines 231 and 236-248.
   - MEMORY.md launch cap is first 200 lines or first 25KB: confirmed by the memory docs at `https://code.claude.com/docs/en/memory` lines 119 and 365-367.
   - Two parts remain weaker than the file implies:
     - `.claude/rules/harness-facts.md:23` states the skill-description rule without the current-doc exception for `disable-model-invocation: true`; the skills docs explicitly say those descriptions are not in context at `https://code.claude.com/docs/en/skills` lines 234 and 308-310.
     - `.claude/rules/harness-facts.md:56` gives an exact project-slug derivation formula, but the memory docs only say the `<project>` path "is derived from the git repository" at `https://code.claude.com/docs/en/memory` lines 351-352. The exact slug format is empirical here, not doc-backed, so its cross-version stability is unverified.

6. CONFIRMED — the bloat lint can be bypassed by multiline YAML descriptions. `extract_description()` only captures the line that starts with `description:` at `.claude/skills/context-budget/scan.sh:70-76`, and `scan_skills()`/`scan_agents()` both trust that single-line result at `.claude/skills/context-budget/scan.sh:110-123` and `.claude/skills/context-budget/scan.sh:146-160`. A future `description: |` block would undercount or count as zero. The current single-line descriptions do match the changelog claim: `/freeze` is 21 words at `.claude/skills/freeze/SKILL.md:3`, and `/investigate` is 23 words at `.claude/skills/investigate/SKILL.md:3`.

7. DISPUTED — I do not see a regression of the old body-counting bug in the eager columns. `scan_agents()` still uses `estimate_frontmatter_tokens()` for eager counts at `.claude/skills/context-budget/scan.sh:108` and keeps full-body totals separate in `lazy` at `.claude/skills/context-budget/scan.sh:109`. `scan_skills()` does the same at `.claude/skills/context-budget/scan.sh:144-145`. The commit diff only added description-word linting to `scan_skills()`.

8. CONFIRMED — these PreToolUse hooks only protect Claude-driven Bash tool calls, not arbitrary host-shell commits. The project wires them into `.claude/settings.json` under the `PreToolUse` matcher for `Bash` at `.claude/settings.json:25-45`. The hooks docs define `PreToolUse` as a hook that runs before Claude processes a tool call, matching tool names like `Bash`, `Edit`, and `Write` at `https://code.claude.com/docs/en/hooks` lines 942-955. A user typing `git commit` directly in a terminal outside Claude does not create a Claude `Bash` tool call, so none of these hooks fire.

## B. Process Compliance

- CHANGELOG.md updated for `b7e7188`: Yes. `git diff 4964299..b7e7188 -- CHANGELOG.md` adds a dedicated `## 2026-04-26 (later)` section describing this prevention bundle.

- GitHub issue created for the prevention bundle: UNVERIFIED, with no positive local evidence. `CLAUDE.md` requires an issue for every feature, bug, or system change at `CLAUDE.md:336-378`. The `b7e7188` commit message does not reference an issue number, unlike earlier issue-backed commits visible in `git log` such as `fbfd25a ... (#91)` and `c44d96e ... (#74)`. I also could not query `gh issue list` locally because GitHub CLI failed with `open C:\Users\mikew\AppData\Roaming\GitHub CLI\config.yml: Access is denied.`

- AGENTS.md counter consistent with REVIEW-LOG.md: Yes by manual comparison. `AGENTS.md:39` says `27 reviews, 71 bugs found`, and `docs/reviews/REVIEW-LOG.md:53` says `27 Codex reviews total, 71 bugs found across codebase.` I attempted to run `bash tools/audit-review-counter.sh` per request, but Git Bash is unusable in this environment (`bash.exe: couldn't create signal pipe, Win32 error 5`), so this is a manual verification rather than a live script run.

- Was `/deep-review` run before shipping: no local evidence, but the literal rule does not require it here. `CLAUDE.md:25` says `/deep-review` is mandatory before commits "touching C#". This commit changes `.claude/`, `AGENTS.md`, `CLAUDE.md`, `CHANGELOG.md`, and `tools/`, not C#. In spirit, some adversarial review happened in the Tier 1 chain (`docs/reviews/REVIEW-LOG.md:98-121` records the prior self-review on `4964299`), but I found no pre-ship deep-review artifact for `b7e7188` itself.

## C. New Findings

[HIGH] `.claude/hooks/check-changelog-changed.sh:36-38` — Prevention bypass — `git commit --amend` is exempted unconditionally, so a user can add fresh `.claude/`, `CLAUDE.md`, or `AGENTS.md` changes to an amended commit without staging `CHANGELOG.md`, defeating the stated "every session" enforcement from `CLAUDE.md:336-345` — Remove the blanket amend exemption or only skip when the amend introduces no new documentation-bearing staged paths.

[MEDIUM] `.claude/hooks/check-claude-files-tracked.sh:34-36` — Prevention bypass — the tracked/gitignored-file gate also skips all `--amend` commits, so a normal amend workflow bypasses the exact safeguard meant to catch another `bin/check-freeze.sh`-style omission — Apply the same on-disk tracked/ignored check to amends.

[LOW] `.claude/rules/harness-facts.md:71` — Documentation drift — the rule says `check-claude-files-tracked.sh` "will warn", but the hook actually hard-denies with `permissionDecision":"deny"` at `.claude/hooks/check-claude-files-tracked.sh:67-75` — Change "warn" to "hard-block" so the rule matches the implementation.

## D. Recursion Risk

Yes. This bundle contains real prevention-theater risk.

- `check-changelog-changed.sh` can false-allow a noncompliant ship path via `--amend` at `.claude/hooks/check-changelog-changed.sh:36-38`.
- `check-claude-files-tracked.sh` has the same `--amend` hole at `.claude/hooks/check-claude-files-tracked.sh:34-36`.
- Both hooks also miss `git -C <path> commit` because they only substring-match `git commit` at `.claude/hooks/check-changelog-changed.sh:29-31` and `.claude/hooks/check-claude-files-tracked.sh:28-30`.

That means a perfectly normal Git workflow can bypass the very gates this commit claims will mechanically stop these failures.

## E. Recommended Follow-Up

- Ship a fourth fix commit for the two real bypasses: remove or narrow the `--amend` exemptions, and broaden commit detection beyond the literal `git commit` substring so `git -C ... commit` is covered.
- Tighten `harness-facts.md` to reflect the current docs precisely: add the `disable-model-invocation: true` exception and label the project-slug rule as empirical rather than source-of-truth.
- Make the counter validator and description linter more resilient: tolerate minor wording drift in the counters, and parse multiline YAML descriptions instead of only single-line scalars.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 3 | LOW: 2
VERDICT: ISSUES FOUND
