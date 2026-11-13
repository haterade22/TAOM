# Session phase boundaries

What to do when one chunk of work in a session ends and the next begins. A phase is a chunk such as the
design talk, the build or the review. Adapted from `skills/engineering/ask-matt/PHASE-BOUNDARIES.md` in
[mattpocock/skills](https://github.com/mattpocock/skills) at `4ae2317dd` (MIT); the record is
[adopt-mattpocock-skills-2026-11-08.md](../reviews/adopt-mattpocock-skills-2026-11-08.md).

Make this decision only at a boundary. In the middle of a phase, continue, or send the remaining
independent work to a subagent.

## The tree

Work from the top. The first yes wins.

1. **Can the session continue?** Yes when the next phase needs this phase's reasoning as it happened, or
   when the next phase fits without reaching auto-compact. Design talk into the build is the usual yes.
2. **Is everything here irrelevant to what comes next?** Then `/clear`. Clearing a session that still
   matters loses the reason behind the work, and reading the diff does not bring that back.
3. **Is the work moving somewhere else?** Then write the state down where the next reader finds it. For
   a Claude session on this machine, such as one in a sibling worktree, use a memory resume card
   (CLAUDE.md "Where new knowledge goes", item 8). Auto memory stays on one machine and in one client, so
   for the other machine, Codex or a person, use a plan file in the repo or the issue.
4. **Can it run without Mike?** A tightly scoped job with no steering, such as a review, goes to a
   subagent under [model-effort-selection.md](../../.claude/rules/model-effort-selection.md). That rule
   keeps most work in the main session, so this question comes after the first three.
5. **Otherwise, `/compact`.** Pass an instruction that names the next phase, for example
   `/compact next we review the Fords driver`, so the summary keeps what that phase needs.

## Why this order

A `/compact` or a handoff replaces the session as it happened with a summary of it, and a summary can
flatten a decision. What survives a compaction is listed in
[harness-loading.md](../../.claude/rules/harness-loading.md) "Context loading": skill bodies come back capped
and path rules come back only on the next matching file access.

Upstream gives about 150k tokens as the point where a model stops reasoning sharply. TAOM has not measured
that figure and does not rely on it.

These are judgement calls. The value is in asking the questions in order, at the boundary.
