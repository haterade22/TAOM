---
name: commit-split
description: Inspect all changed files, group by logical concern, and guide atomic per-concern commits following TAOM's 50/72 rule and commit trailer convention.
---

# Commit Split

Review all changed files, split them into logical atomic commits, and execute each one in sequence. Produces a clean, reviewable git history for multi-file session changesets.

## Step 1: Inspect Current State

```bash
git status
git diff --name-only
git diff --name-only --cached
git ls-files --others --exclude-standard
```

List ALL files: staged, unstaged, and untracked. Present a consolidated list.

## Step 2: Propose Logical Groups

Group files using these TAOM-specific heuristics. One group = one commit.

| Files | Commit type | Example message |
|-------|------------|-----------------|
| `Main/Features/<Name>/**/*.cs` | `feat:` or `fix:` | `feat: add alignment-aware execution penalties` |
| `TAOM.Tests/Features/<Name>/**` | `test:` | `test: add AlignmentService coverage` |
| `Main/_Module/ModuleData/**/*.xml`, `*.xslt` | `data:` | `data: add gondor equipment templates` |
| `docs/features/*.md` | `docs:` | `docs: add alignment-aware-execution feature doc` |
| `CHANGELOG.md` | `docs:` | `docs: update changelog for execution system` |
| `CLAUDE.md`, `.claude/**` | `chore:` | `chore: update claude code tooling` |
| `Main/IoC.cs`, `Main/SubModule.cs` | `chore:` | `chore: register alignment feature in IoC` (or bundle with feat:) |
| `docs/adrs/**` | `docs:` | `docs: add ADR-010 for execution alignment approach` |
| Bug fixes spanning multiple files | `fix:` | `fix: resolve null ref in WargAttackService` |

**Rules for grouping:**
- Tests written BEFORE implementation (TDD) → bundle with their feature commit
- Tests written AFTER implementation → separate `test:` commit
- `IoC.cs` / `SubModule.cs` wiring → bundle with the feature commit it wires in
- If a single logical feature spans C# + XML data, keep together in one `feat:` commit
- Never split a feature from its adapter if the adapter was created for that feature

**Present the proposed groups as a numbered list:**
```
Proposed commits (N total):
1. feat: [message] — [files]
2. test: [message] — [files]
3. data: [message] — [files]
4. docs: [message] — [files]
5. chore: [message] — [files]
```

Ask: "Does this grouping look right? Any changes before I start committing?"

## Step 3: Execute Commits in Order

For each proposed commit:

1. **Stage exactly** the files for this group:
   ```bash
   git add [file1] [file2] ...
   ```

2. **Review staged diff** to confirm correct files:
   ```bash
   git diff --cached --stat
   ```

3. **Apply trailers** — for each commit, assess which optional trailers apply:
   - `Constraint:` — if a TaleWorlds limitation forced a suboptimal approach
   - `Rejected:` — if a notable alternative was considered and dropped
   - `Not-tested:` — if Harmony patches or in-game behavior can't be unit tested
   - `Research:` — if a TaleWorlds class was decompiled to inform this change
   - `Save-compat:` — if this change affects save file compatibility

4. **Write commit** (50-char subject, 72-char body wrap, no AI attribution):
   ```bash
   git commit -m "$(cat <<'EOF'
   [type]: [subject under 50 chars]

   [Optional body — context, why, not what]

   [Optional trailers]
   EOF
   )"
   ```

5. **Confirm**: `git log --oneline -1` — show user what was committed.

Repeat for each group.

## Step 4: Final Check

After all commits:

```bash
git log --oneline -N   # N = number of commits made
git status             # should be clean
```

Remind: run `/verify` to confirm build + tests still pass after the commit split.
