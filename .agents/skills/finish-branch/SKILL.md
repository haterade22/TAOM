---
name: finish-branch
description: Integrate a merge-ready branch into the trunk — fast-forward check, merge, regenerate backlinks, CHANGELOG, delete branch (local+remote), push with confirmation. TAOM trunk-based, not Git Flow.
argument-hint: [branch] [base=bannerlord-1.4.5]
---

# Finish Branch (trunk integration)

Integrate a completed branch into TAOM's trunk (`bannerlord-1.4.5`, the de-facto master). This is the **post-`/ship`** step: `/ship` gates that a feature is review-clean; this skill does the actual git integration + doc-consistency cleanup that we otherwise run by hand and forget steps in.

**Not** `git-workflow:finish` — that's Git Flow (develop/feature/release/hotfix + version tags). TAOM is trunk-based: ephemeral `taom-*` branches merge into `bannerlord-1.4.5`, no tagging.

## When to invoke

- A branch's commits are reviewed and ready to land on `bannerlord-1.4.5`.
- **Skip for** a branch that still needs `/ship` (run that first), or work that was committed directly to trunk (no branch to finish).

## Steps

### 1. Pre-flight (read-only — never skip)
- `git status --short` — working tree must be clean. If dirty, stop and surface (the user may have parallel in-flight work that shouldn't ride along).
- Confirm the base: default `bannerlord-1.4.5`. If `$ARGUMENTS` names a different base, use it.
- **Fast-forward check:** `git log --oneline <base> ^<branch>` — empty output = base hasn't advanced past the branch point, so the merge is a clean FF. Non-empty = base moved; it'll be a real merge that **may conflict** — surface the divergence and preview with `git merge --no-commit --no-ff` (then `git merge --abort`) before committing. Never force.

### 2. Merge
- `git checkout <base> && git merge <branch>` — expect "Fast-forward".

### 3. Regenerate backlinks (doc consistency)
- `python tools/build_backlinks.py` — the merge may have introduced docs whose `## Referenced by` footers drifted.
- **Only stage footer changes for files the merged branch actually touched.** If `build_backlinks.py` also wants to update footers in *unrelated* docs (parallel-session work), that's not this branch's concern — leave those for the owning workstream. Commit the in-scope footer updates as `docs(backlinks): regenerate footers for <area>`.

### 4. CHANGELOG
- If the merged branch didn't already include a CHANGELOG entry, add one under today's date summarizing the landed work. Commit.

### 5. Delete the merged branch
- `git branch -d <branch>` — the `-d` (not `-D`) refuses if the branch isn't fully merged; that safety is intentional, don't override with `-D`.
- `git push origin --delete <branch>` — only after local delete succeeds (confirms it was merged).

### 6. Push the trunk
- **Confirm with the user before pushing `bannerlord-1.4.5`.** It's the becoming-master branch; `validate-push.sh` warns on master/main pushes. Do not auto-push — surface "ready to push N commits" and wait, unless the user pre-authorized the push for this run.

## Gotchas

- **FF vs real merge:** the step-1 check is the whole game. A clean FF is safe and trivially reversible (`git reset --hard <branch>@{1}`); a conflicting real merge needs human eyes. Surface, don't force.
- **Parallel-work footer drift:** `build_backlinks.py` regenerates the *whole* tree. After a merge there are often footer updates in docs the branch never touched (another session's lord-skills/bandit work). Don't sweep those into this commit — `git add` only the docs in the branch's diff.
- **`git branch -d` as a merge gate:** if it refuses, the branch isn't actually merged into the current base — investigate, don't `-D`.
- **CHANGELOG hook:** if the branch touched `.Codex/`, `AGENTS.md`, or `AGENTS.md`, the pre-commit hook (`check-changelog-changed.sh`) requires CHANGELOG.md in the post-merge commit set — usually already satisfied by the branch's own CHANGELOG entry.
- **Push is shared state:** the user has historically run trunk pushes themselves. Default to offering, not doing.

## See also

- `.Codex/skills/ship/SKILL.md` — the pre-merge completion gate this skill follows.
- `tools/build_backlinks.py` — step-3 backlink regeneration.
- `.Codex/hooks/validate-push.sh` — the push guard referenced in step 6.
