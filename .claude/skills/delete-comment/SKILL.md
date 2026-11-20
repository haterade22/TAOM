---
name: delete-comment
description: "Use when asked to sweep, audit or delete code comments in a folder or feature. Classifies against the ADR-009 keep-list, moves decisions to docs, files excuse issues, lowers the ratchet."
argument-hint: <folder, for example Main/Features/Refuge>
---

# Comment sweep

Sweep the `//` and `/* */` comments of one folder against the [comment rubric](../../../docs/reference/comment-rubric.md)
(ADR-009 keep-list).
`CommentRatchetGateTests` holds the result: each file's count may only fall.

## Phase 1: scope

1. Claim it: `python tools/work_claims.py claim comment-sweep-<folder>`.
2. Work in a sibling worktree branch, never on trunk.
3. Pick ONE folder. `python tools/comment_ratchet.py --report` lists folders by comment lines. Record the
   folder's "before" number: `python tools/comment_ratchet.py --report --top 0 | grep <folder>`.
4. `git status` the folder. Skip a file with another session's uncommitted edits.
5. `Main/SubModule.cs` and `Main/IoC.cs` are single-owner: sweep each in its own commit, and never in a parallel fan-out.

## Phase 2: classify

Read each comment with the code around it. Put each one in one category of the rubric, then act:

| Category | Action |
|---|---|
| Tautology | Delete |
| Stale | Delete. If it guards real behaviour with a wrong engine citation, re-check with `pwsh tools/taom-src.ps1` and fix it |
| Excuse | Delete. Record `file:line` and one line of what the workaround does for the folder's issue |
| Decision record | Move to `docs/features/<name>.md` (an ADR only for an architecture decision), then delete. A "must stay" or "do not remove" comment becomes a test |
| Engine fact | Keep. Cut it to the constraint and its citation |
| Marker, licence, contract | Keep |

When unsure between Engine fact and Excuse, ask: can TAOM change the code so the comment is not needed? Yes is an Excuse.

## Phase 3: apply

1. Edit with the Edit tool. Never `sed -i` a CRLF file.
2. Never delete a gate marker: `// swallow-ok: <reason>` (`SilentCatchRatchetTests`).
3. Delete commented-out code: it is Stale.
4. Keep a `///` summary on an interface or public contract. Shorten a long one to one sentence unless it records an engine fact, a citation or why a gate exists.
5. Leave blank-line and brace layout clean after a deletion: no empty line pairs, no orphan blank line at the top of a block.

## Phase 4: record

1. Excuses: one GitHub issue per folder, label `enhancement`, title `Workarounds in <folder> (comment sweep)`, with the list from Phase 2. Name the issue in the commit body.
2. Decision records: the feature doc edit goes in the same commit.
3. Check that `git status` shows no untracked `.cs` file that is not yours: `--regen` scans the disk, and a
   stray file in the baseline fails every clean clone. Then run `python tools/comment_ratchet.py --regen`.
4. Record the "after" number: `python tools/comment_ratchet.py --report --top 0 | grep <folder>`.
5. A shorter entry point fails `EntryPointLineCountGateTests` (shrink-only baseline). Lower it:
   `TAOM_WRITE_ENTRY_POINT_BASELINE=1 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests.Baseline_Regenerate_WhenAsked"`.
   If a class fell under 150 lines, its row leaves the baseline: set `MaxBaselineEntries` in
   `EntryPointLineCountGateTests.cs` to the `# Entries: N` number in the regenerated file's header.

## Phase 5: verify and commit

1. `dotnet build` the solution with `-p:DisableModuleCopy=true -p:ModuleId=`. A deleted comment cannot change behaviour, but a slip can break syntax.
2. The full suite: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`. Source-text tests read comment text and catch a deleted marker.
3. `/deep-review` (low risk tier: comment-only diff).
4. Commit with explicit paths. Subject `chore(comments): vX.Y.Z - sweep <folder>`. The body gives the category counts and the lines removed.
