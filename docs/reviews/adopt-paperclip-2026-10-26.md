# Paperclip adoption review (2026-10-26)

Review of [paperclipai/paperclip](https://github.com/paperclipai/paperclip) against TAOM's AI
harness, through `/adopt-external`. Mike chose the items below on 2026-10-26. Issues
[#1008](https://github.com/haterade22/TAOM/issues/1008) and
[#1009](https://github.com/haterade22/TAOM/issues/1009).

## Coverage

- **Repository:** `master` at `a67a18c` (11,988 files), MIT. Read through a shallow clone in the
  session scratchpad, deleted afterwards; nothing in it was installed or run. Read at source:
  `README.md` and the Claude Code adapter (`packages/adapters/claude-local/`).
- **TAOM:** token usage and commits from every local transcript and `git log` since 2026-08-27.

## What Paperclip is

A Node.js server and React UI that runs a "company" of agents: tickets with one assignee each,
heartbeats that wake agents for assigned work, budgets with alerts and automatic pauses, approval
gates, and sessions kept across reboots. It drives Claude Code, Codex and other CLIs through
adapters.

## Security

- **Safe to learn from:** yes.
- **Safe to install:** no. The Claude adapter defaults `dangerouslySkipPermissions` to true, which
  passes `--dangerously-skip-permissions` to every unattended run
  (`packages/adapters/claude-local/src/index.ts:71`), and the chat route hard-codes the flag. TAOM's
  hooks would still run, but in an unattended run an ask becomes a deny that nobody sees.
  Telemetry is on by default (opt out with `PAPERCLIP_TELEMETRY_DISABLED=1` or `DO_NOT_TRACK=1`).

## Measurements

**Where the token budget goes** (section 10 of `tools/harness_retro.py`, 2026-10-19 to
2026-10-26, each request counted once):

| Agent and model | Requests | Share of cache creation plus output |
|---|---|---|
| deep-reviewer / claude-opus-5-5 | 70,106 | 54.0% |
| main / claude-opus-5-5 | 12,987 | 21.6% |
| implementer / claude-sonnet-5-5 | 10,808 | 14.6% |
| feature-builder / claude-sonnet-5-5 | 4,329 | 4.3% |
| architect / claude-opus-5-5 | 5,865 | 4.1% |

816 `deep-reviewer` agents ran in those 7 days, about 15 M tokens each, mostly cache re-reads.
The risk tiers of 2026-10-22 moved the review share of daily tokens only from 50-63% to 51-58%.
569 `implementer` requests ran on Opus, a model its definition does not set.

**Duplicate work between sessions.** Of 1,043 trunk commits since 2026-08-27, only 3 issues got
commits from two main sessions whose activity overlapped (#964, #731, #371), and those look like
planned work. 13 subjects landed twice: 10 pairs from 2026-09-24 are different patches from the
`/improve` plan merges; the 2 pairs from 2026-10-23 are identical patches, the same commit landed
twice.

## Decisions

| # | Item | Decision |
|---|---|---|
| A1 | Budget tracking | Adopted as a measurement, not a server (#1008): section 10 gains tokens by agent type and model, model overrides, the costliest transcripts and a growth check against the previous run |
| A2 | Task claim between sessions (one owner per task) | Not built: 3 shared issues in 60 days do not justify it. The real defect was a commit landing twice, so the changelog generator now lists an exact repeat once (#1009) |
| A3 | Review cost | Mike chose to measure first: the risk tiers are new and #1003 cut reviewer context, so the next retro decides with section 10's numbers. Proposals then: review per push instead of per commit, or high effort for tier 2 |
| X1 | Install Paperclip or its adapters | Declined: unattended runs with permissions skipped by default, telemetry on |
| X2 | Org charts, heartbeats, approval gates, goal ancestry | Declined: GitHub issues, `/loop`, the hooks and Mike's word already cover them |

---
