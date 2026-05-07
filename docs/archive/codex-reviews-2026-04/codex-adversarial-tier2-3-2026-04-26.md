NEEDS FIXES — 1 HIGH / 2 MEDIUM / 2 LOW

## A. KNOWN SUSPECTS RESPONSE

1. **Inline-hook activation claims in new skills**
   - **`/context-save`: DISPUTED (no bad hook-activation claim found).** Frontmatter has no `hooks:` block at [.claude/skills/context-save/SKILL.md](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/context-save/SKILL.md:1), and the body only claims file write + pairing with `/context-restore` and `/compact` at [context-save](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/context-save/SKILL.md:111). It does **not** claim that writing the snapshot activates any hook.
   - **`/context-restore`: DISPUTED.** Frontmatter has no `hooks:` block at [context-restore](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/context-restore/SKILL.md:1), and the body presents it as read-only loader logic at [context-restore](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/context-restore/SKILL.md:23). No hook requirement is claimed.
   - **`/agent-introspection-debugging`: DISPUTED.** Frontmatter has no `hooks:` block at [agent-introspection-debugging](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/agent-introspection-debugging/SKILL.md:1). Its Phase 2/3 text correctly treats freeze as active only when the deny message is already happening and points users to `/freeze` or `/unfreeze` rather than claiming state-file writes activate hooks at [agent-introspection-debugging](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/agent-introspection-debugging/SKILL.md:63) and [agent-introspection-debugging](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/agent-introspection-debugging/SKILL.md:107).
   - **`/skill-stocktake`: DISPUTED.** Frontmatter has no `hooks:` block at [skill-stocktake](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/skill-stocktake/SKILL.md:1), and the skill is read-only audit text.
   - **Doc-backed lifecycle check:** Claude’s hooks docs say hooks declared in skills/agents are “scoped to the component’s lifecycle and only run when that component is active.” Source: https://code.claude.com/docs/en/hooks

2. **`effort:` field correctness**
   - **`/deep-review effort: high`: PARTIAL / UNVERIFIED on child-agent impact.** The docs say `effort` “overrides the session effort while this skill is active” and defaults to inheriting the session; they do **not** say whether spawned subagents inherit a skill-level override when the skill text itself supplies only per-agent `model:` directives. Source: https://code.claude.com/docs/fr/skills (frontmatter reference, `effort`). That means the field is definitely not dead for the parent skill’s own reasoning at [deep-review](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/deep-review/SKILL.md:41), but whether it materially changes the spawned review agents is **UNVERIFIED** from docs.
   - **`/scope-check effort: low`: CONFIRMED risk.** Unlike `/deep-review`, `/scope-check` does its own judgment inline, including the new anti-silent-scope-drop classification workflow at [scope-check](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/scope-check/SKILL.md:14) and [scope-check](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/scope-check/SKILL.md:55). The docs say `effort` overrides the active skill’s reasoning budget. Source: https://code.claude.com/docs/fr/skills. For complex scope calls, `low` is directly applied to the exact reasoning path that is supposed to prevent silent omission.

3. **Subagent boundary integrity**
   - **`debugger`: mostly clean, with one routing ambiguity edge.** The agent body draws a clear boundary against TAOM C# bugs and agent-loop problems at [debugger](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/agents/debugger.md:15), and CLAUDE.md routes non-TAOM script/build/hook problems there at [CLAUDE.md](/abs/path/c:/Users/mikew/source/repos/TAOM/CLAUDE.md:115). For “my Python script is failing because of a Bannerlord DLL path,” the routing still resolves to `debugger` because the failure surface is tooling/script, not TAOM C#. I do **not** consider that boundary broken.
   - **`refactoring-specialist`: clean.** Its boundary against `/deslop`, `code-architect`, and `feature-builder` is explicit at [refactoring-specialist](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/agents/refactoring-specialist.md:17), and it matches the existing `/deslop` deletion-first contract at [deslop](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/deslop/SKILL.md:45).
   - **`error-detective`: clean.** It is explicitly for multi-symptom / cross-system correlation at [error-detective](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/agents/error-detective.md:13), and tells the caller to fall back into one or more `/investigate` runs after correlation at [error-detective](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/agents/error-detective.md:63). For “save-load corruption AND mission-init crash AND companion-spawn freeze,” this agent should go first.

4. **`suggest-compact.sh` boundary detection edge cases**
   - **`git commit`: CONFIRMED for bare form only.** It matches `*"git commit"*` at [suggest-compact.sh](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/hooks/suggest-compact.sh:76).
   - **`git -C path commit`: CONFIRMED MISS.** No `*"git -"*" commit"*` pattern exists in this hook, unlike the reference pattern codified in [harness-facts.md](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/rules/harness-facts.md:74).
   - **`git -c key=val commit`: CONFIRMED MISS.** Same reason and same file lines.
   - **`git commit-tree` / `git commit-graph`: CONFIRMED REJECT.** The inner `*"git commit-"*` rejection at [suggest-compact.sh](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/hooks/suggest-compact.sh:79) correctly avoids those plumbing commands.
   - **Build/test/push substring false positives: CONFIRMED RISK.** `*"./build.ps1"* | *"dotnet build"* | *"dotnet test"* | *"git push"*` at [suggest-compact.sh](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/hooks/suggest-compact.sh:84) and [suggest-compact.sh](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/hooks/suggest-compact.sh:88) are raw substring matches on the whole command string, so a command that merely mentions those literals in an argument can trip the boundary hint.
   - **Throttle reset: DISPUTED (works as written).** The hook persists the last boundary count at [suggest-compact.sh](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/hooks/suggest-compact.sh:95) and only re-fires when `COUNT - LAST_BOUNDARY > 10` at [suggest-compact.sh](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/hooks/suggest-compact.sh:68). That permits repeated hints in long sessions; it is not stuck.

5. **`/context-save` state file location safety**
   - **Gitignore status: CONFIRMED SAFE.** `git check-ignore -v .claude/state/context/hypothetical.md` resolves to `.claude/state/.gitignore:1:*`. The ignore file itself is [state/.gitignore](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/state/.gitignore:1).
   - **`core.excludesfile` concern: DISPUTED.** Repo-local `.gitignore` already ignores the path. A different global excludes file can add ignores, not remove this repo-local ignore. Accidental tracking would require an explicit force-add, not a different `core.excludesfile`.
   - **Freeze interaction note: CONFIRMED BUG.** `/context-save` claims that if you freeze to `.claude/`, the snapshot write “will be blocked” at [context-save](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/context-save/SKILL.md:126). `check-freeze.sh` allows writes inside the freeze boundary at [check-freeze.sh](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/freeze/check-freeze.sh:140). If `FREEZE_DIR` is `<repo>/.claude`, then `<repo>/.claude/state/context/foo.md` is inside boundary and allowed.

6. **`/skill-stocktake` checklist completeness**
   - **Amend-exemption recursion-risk check: CONFIRMED MISSING.** The checklist at [skill-stocktake](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/skill-stocktake/SKILL.md:35) does not include the “amend exemptions in pre-commit hooks” rule codified in [harness-facts.md](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/rules/harness-facts.md:86).
   - **`effort:` validity: CONFIRMED PRESENT.** It is explicitly checked at [skill-stocktake](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/skill-stocktake/SKILL.md:41).
   - **`triggers:` drift: CONFIRMED PRESENT.** It is explicitly checked at [skill-stocktake](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/skill-stocktake/SKILL.md:39).
   - **DOC-BACKED vs EMPIRICAL labeling: CONFIRMED MISSING.** The checklist never asks whether facts are labeled per the convention added to [harness-facts.md](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/rules/harness-facts.md:114).

7. **Scope-reduction prohibition rule enforceability**
   - **CONFIRMED ASPIRATIONAL, not deterministic.** The new rule is pure prose at [scope-check](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/scope-check/SKILL.md:55). It has no hook, no state file, no plan artifact, and no later verifier comparing promised scope vs delivered scope. This means it depends on Claude following its own instruction; it is not mechanically enforceable.

8. **`/context-restore` discrepancy detection**
   - **PARTIAL.** The skill does include a literal cross-check step at [context-restore](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/skills/context-restore/SKILL.md:45), so this is not an empty promise. But it is still high-level guidance, not a deterministic procedure with explicit commands for branch diff, file existence, and modified/unmodified reconciliation. I would call the current state “present but operator-dependent,” not “missing.”

## B. PROCESS COMPLIANCE

- **CHANGELOG updated for `79350f2`: YES.** Verified by `git diff 2c4d414..79350f2 -- CHANGELOG.md`; the new entry is at [CHANGELOG.md](/abs/path/c:/Users/mikew/source/repos/TAOM/CHANGELOG.md:3).
- **Issue `#93` referenced in commit message: YES.** The commit subject is `feat(.claude): adopt Tier 2 + Tier 3 picks from ecosystem review (#93)` per `git log -1 79350f2`.
- **AGENTS.md counter consistent with REVIEW-LOG.md: YES, by manual comparison.** Git Bash was not usable in this environment (`bash.exe` failed before `tools/audit-review-counter.sh` could run), so I compared [AGENTS.md](/abs/path/c:/Users/mikew/source/repos/TAOM/AGENTS.md:40) against [REVIEW-LOG.md](/abs/path/c:/Users/mikew/source/repos/TAOM/docs/reviews/REVIEW-LOG.md:54). Both say `28` reviews / `77` bugs.
- **Was a deep-review run before shipping? No local evidence found.** The hard rule is C#-specific at [CLAUDE.md](/abs/path/c:/Users/mikew/source/repos/TAOM/CLAUDE.md:25), so this `.claude/`-only commit did not violate the literal wording. I found no pre-ship artifact showing a `/deep-review` run for `79350f2`; the files I found under `.claude/tmp/` are post-hoc review artifacts from this review pass.
- **Scope note:** the shipped diff also modifies [.claude/settings.local.json](/abs/path/c:/Users/mikew/source/repos/TAOM/.claude/settings.local.json:32), which was not listed in the user’s changed-file summary.

## C. NEW FINDINGS

### HIGH

- [HIGH] `.claude/hooks/suggest-compact.sh:76` — Harness-facts regression / recursion-risk — the new boundary-aware matcher only catches bare `git commit` substrings and misses `git -C path commit` and `git -c key=val commit`, even though those exact forms were codified as mandatory after review #28 in `.claude/rules/harness-facts.md:58-84` — Fix: use the same two-stage commit detection pattern already standardized in `harness-facts.md`.

### MEDIUM

- [MEDIUM] `.claude/skills/scope-check/SKILL.md:55` — Enforcement gap — the new “do not silently drop Y” rule is prose-only and not mechanically checkable against any plan/spec artifact, so it cannot actually prevent the class of bug it claims to prohibit — Fix: if this rule is meant as prevention rather than guidance, it needs a deterministic verifier somewhere in the workflow.
- [MEDIUM] `.claude/skills/skill-stocktake/SKILL.md:31` — Audit baseline drift — the checklist omits both the review-#28 amend-exemption recursion-risk check and the DOC-BACKED vs EMPIRICAL labeling convention now required by `.claude/rules/harness-facts.md:86-115`, so the audit skill can certify a stale harness against an outdated checklist — Fix: extend the checklist to include those two post-#28 requirements.

### LOW

- [LOW] `.claude/skills/context-save/SKILL.md:126` — Behavior claim mismatch — the note says freezing to `.claude/` blocks snapshot writes, but `.claude/state/context/...` is inside that boundary and `check-freeze.sh` allows in-boundary writes at `.claude/skills/freeze/check-freeze.sh:140-143` — Fix: correct the note so it only warns about freeze scopes that exclude `.claude/state/context/`.
- [LOW] `CHANGELOG.md:46` — Documentation drift — the entry says the counter validator “will auto-bump REVIEW-LOG → AGENTS.md,” but `tools/audit-review-counter.sh` only updates AGENTS.md when run with `--fix` at `tools/audit-review-counter.sh:75-95` — Fix: change the claim to “can auto-bump with `--fix`” or document the wrapper that actually invokes it.

## D. RECURSION RISK

- **`suggest-compact.sh` commit-form miss:** YES, the productivity claim is partially defeated. The bundle explicitly marketed boundary-aware compaction on `git commit`, and this hook repeats the exact `git -C` / `git -c` blind spot already RCA’d in review #28. For sessions that use those forms, the “boundary-aware” behavior is theater.
- **`scope-check` prose-only prohibition:** YES, partially defeated. The change was sold as a prohibition against silently dropping scope, but there is no deterministic enforcement. This improves guidance, not prevention.
- **`skill-stocktake` stale checklist:** YES, partially defeated. An audit skill that omits the last review’s codified harness lessons can rubber-stamp the same bug class later.
- **`context-save` freeze note mismatch:** NO, this is not a prevention failure. The skill still writes safely; the note is just wrong and may cause unnecessary `/unfreeze`.
- **CHANGELOG auto-bump claim:** NO, this is doc drift, not a runtime defeat. The validator still works when invoked correctly.

## E. RECOMMENDED FOLLOW-UP

- **Fix commit required.** The `suggest-compact.sh` matcher miss and the two MEDIUM harness-quality gaps are still open.
- **Loop not closed.** The highest-value fix is to make the new boundary-aware compaction logic conform to the already-pinned `harness-facts.md` commit-pattern rules, then update `/skill-stocktake` and clarify the `/scope-check` rule’s enforcement level.
- **No evidence of a shipping-blocking runtime bug in the new skills themselves beyond the hook matcher gap.** Most of the remaining issues are harness-truthfulness problems: guidance presented as enforcement, or checklist/documentation drift.
