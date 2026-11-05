# Commit gates

## Overview

The gates that judge a commit before it lands: its message, its doc budgets and rule text, its ModuleData, and any harness file gitignore would lose. They run from `.githooks/`, so they bind every committer whose clone sets `core.hooksPath`, a terminal or an IDE included. This is tooling: it changes nothing in the game.

## Why This Exists

- **Before:** only `commit-msg` bound every committer. The other gates were Claude Code hooks, which run only on a commit Claude makes, and CI left on 2026-10-05. 41 commits skipped them, and after two plain git commits [trunk carried red gates for five days](../reviews/rca-harad-repair-2026-10-15.md).
- **Requirement:** a gate that claims to protect the repo holds for whoever commits.
- **Without it:** a commit from the IDE grows the entry docs past their budget, or removes a troop that a save still holds, and nothing says so until a later session trips over it.

## Architecture

### Design Challenge

Git hands a hook the final index (a temporary one for a path commit) with the work tree as cwd. So a hook parses no command, which removes a whole class of Claude gate bugs. But it runs under whatever git the committer has. The git inside Visual Studio and GitHub Desktop ships `sh`, `grep`, `sed`, `tail` and `printf`, with no `bash` and no GNU `timeout`, and `env bash` finds the WSL launcher there.

### Solution Approach

- **POSIX `sh`,** so the hooks run under each git. With no GNU `timeout` the gates run with no time limit.
- **A clean merge skips `pre-commit`,** so `pre-merge-commit` runs the same script with `TAOM_CLEAN_MERGE=1`. Git writes `MERGE_HEAD` only after that hook, so the flag tells `lint_docs` what it could otherwise read from there.
- **Fail open, and say so.** Only exit 1 refuses. A missing Python, a crash, an overrun or any other exit prints "NOT checked" and lets the commit through, because a broken hook must not block work.
- **`lint_docs --staged` reads the tree the commit records** (`CommitView`), so another session's unstaged edit cannot refuse a commit, and a staged breach cannot pass behind a clean working copy.

### What runs

| Hook | Runs when | Gate | Reads |
|---|---|---|---|
| `commit-msg` | every commit | `tools/check_commit_msg.py`: the versioned subject, its length, no AI attribution, one closing keyword per issue | the message, and the `<Version>` of the tree being committed |
| `pre-commit`, `pre-merge-commit` | every commit | a file under `.claude/` that gitignore keeps out | the work tree |
| | a surface `lint_docs` reads is staged | `tools/lint_docs.py --drift-only --staged`, about 1 s | the tree the commit records |
| | ModuleData XML or XSLT is staged | `tools/validate_moduledata.py` with the listed codes, and `tools/generate_career_kits.py --verify`, about 12 s together (2026-11-04) | **the working tree** |

The list of validator codes in the hook equals the list in the Claude gate, and `tools/tests/test_githooks_pre_commit.py` holds them equal.

### Known limits

- **The ModuleData gates read the working tree, not the index.** A staged bad commit can pass behind a good working copy, and another session's unstaged bad XML can refuse a good commit. The hook names any ModuleData file with unstaged edits. The Claude gate has the same limit.
- **The hook cannot see an amend.** It judges only what an amend newly stages, and it does not re-judge HEAD's own files.
- **A person may pass `--no-verify`.** `block-no-verify.sh` refuses it for Claude only.
- **A clone that never set `core.hooksPath` runs none of this.** `setup-dev-env.ps1` sets it, `CONTRIBUTING.md` says so, and the session banner reports a wrong value.

### Stage two

The hooks run beside the Claude commit gates until about 2026-11-17. Then compare them, and if they agree, delete `check-moduledata-validation.sh`, `check-doc-config-drift.sh`, `check-claude-files-tracked.sh` and `check-commit-subject-version.sh`, with their parser code, except `taom_git_repo_dir`, which `block-broad-git-add.sh` needs. Repoint `CodeList` in `tools/tests/test_githooks_pre_commit.py`, and the matching check in `tools/tests/test_validate_moduledata.py`. Decide the two limits above first, because the Claude gates see an amend and the hooks do not.

## Configuration

`git config core.hooksPath .githooks`, set by `setup-dev-env.ps1`.

## Key Files

| File | Purpose |
|------|---------|
| `.githooks/commit-msg` | The message gate |
| `.githooks/pre-commit` | The content gates |
| `.githooks/pre-merge-commit` | Runs `pre-commit` for a clean merge |
| `tools/check_commit_msg.py` | The message rules |
| `tools/lint_docs.py` | The doc budgets, version, config and rule text checks ([doc-health linter](doc-health-linter.md)) |
| `tools/validate_moduledata.py` | The ModuleData checks ([validation](moduledata-validation.md)) |
| `.claude/hooks/block-no-verify.sh` | Refuses a Claude commit that would skip the hooks |
| `tools/tests/test_githooks_pre_commit.py` | Commits in throwaway repositories, through each bundled git installed here |

## How to verify in game

Not applicable: tooling. To check it by hand, stage a doc that cites a `feedback_` memory under `.claude/rules/` and commit: the commit is refused, and the finding prints. Then unstage it.

## Related

- [Hooks catalog](../reference/hooks-catalog.md): every hook, with who it covers.
- [ADR-011](../adrs/011-knowledge-delivery-tiers.md): the budgets.
- [The review of these gates](../reviews/rca-rules-review-2026-11-03.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/harness-retro-2026-11-03.md](../reviews/harness-retro-2026-11-03.md)

<!-- backlinks-end -->
