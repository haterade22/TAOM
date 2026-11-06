# Harness² adoption review (2026-10-21)

Review of [google-research/harness2](https://github.com/google-research/harness2) (Harness²,
Apache-2.0) against TAOM's AI harness, through `/adopt-external`. Mike asked what the harness can
take from it and wants the harness to keep improving itself. He approved the plan as a whole on
2026-10-21; issue [#962](https://github.com/haterade22/TAOM/issues/962).

## Coverage

- **Repository:** `1cfe254e` (2026-10-13, "Fix arXiv link"), 114 files, read through a blobless
  clone in the session scratchpad. Nothing was installed or run. Read in full: `README.md`,
  `docs/EXPERIMENTS.md`, `docs/BENCHMARKS.md`, `pyproject.toml`, `harness2/install.py`,
  `harness2/preflight.py` and every module in `harness2/core/` except `schedule.py`, which was read
  by its function list. `benchmarks/`, `cli/`, `substrate/` and `third_party/` were covered by the
  security pass and the tree listing only: they are runners for three public benchmarks.
- **Paper:** arXiv 2610.14224 v1 (2026-10-09), its HTML converted to text and the ablation tables
  read directly. A summarizing fetch was used first; every number below was re-read from the
  paper's own text.
- **TAOM:** the lessons archive, RCAs, `/improve`, the hooks in `.claude/settings.json`, the agent
  definitions, auto memory, `REVIEW-LOG.md` with `tools/analyze_reviews.py`, and the earlier
  `adopt-*` records, inventoried by a read-only agent and spot-checked.

## What Harness² is

For each task it runs the base harness k times. Two scoped editors then edit the harness from
those runs: one owns how the agent works (procedure, tools, scripts, hooks), the other what a
practitioner knows (domain skills and facts). Each edited harness runs once, a composer builds a
final harness from the behaviour contrasts, and a blind selector picks the output to deliver. A
reflector then writes a row per edit (component, change, observed effect, verdict: helped, hurt,
no observed effect or confounded) and at most one transferable lesson (cue, lesson, evidence,
confidence), or nothing. The adopted harness and its lesson go into a per-domain library that
later tasks read.

The paper's ablations (Gemini 3.5 Flash under OpenCode):

- **Table 3** (k=1): without the library, WorkBuddy-Bench 77.5 to 74.4 and JobBench 36.0 to 34.2;
  without the scoped editors, 74.6 and 32.3; without composition, 75.9 and 34.1.
- **Table 6** (WorkBuddy-Bench, k=5, components removed from the improved harness): skills cost
  5.3 points on average, scripts 3.7, the prompt group 1.5. Every ablated harness still beat the
  base (72.4).
- **Figure 7:** refining the harness directly, with no runs, probes or library to read, made it
  worse: Code 73.4 to 63.2, Web 69.9 to 65.0.

The last result is the one TAOM leans on: harness edits made without evidence regressed.

## Security

- **Safe to learn from:** yes. `pyproject.toml` declares no runtime dependencies and no build
  hooks; the optional extras are pinned. Setup clones dependencies at pinned SHAs and applies
  local patches. No telemetry, no outbound calls besides the model endpoints and setup downloads
  (`socket` in `cli/bridge.py` and `substrate/codex.py` picks a local port), no decoded payload
  is executed (`oc_harness_agent.py` decodes a JSON config).
- **Safe to install here:** no. The improver runs Claude Code with permissions skipped
  (`core/proposer.py:216`), Codex runs with its sandbox off, executor network access is open by
  design (README "Web access"), and setup runs `npm install` and a Hugging Face download at an
  unpinned revision.
- **Automated vet:** `python tools/audit_claude_config.py --root <clone> --external` scanned all
  114 files: HIGH 13, MED 27, INFO 2. The HIGH rows are the permission-skip flag (three hits),
  workspace `rmtree` calls and a Dockerfile apt cleanup (seven), a reveal pattern in a runner's
  prompt renderer, and two argument-list subprocess calls (`benchmarks/jobbench.py:1137` runs the
  benchmark judge with config values; `cli/bridge.py:104` starts the LiteLLM proxy). Both were
  read at source; neither passes model output to a shell. The MED rows are plain subprocess calls.
- Nothing was ported as a file, so no upstream code or Apache-2.0 text is vendored. The ideas
  below are rewritten in TAOM's words and credited here and in the
  [provenance register](../reference/provenance-register.md) (behavioural port: the prompts were
  read while the procedure was written).

## Already in TAOM

| Harness² | TAOM |
|---|---|
| Per-domain experience library | 13 lesson categories loaded by path rules, in the repo, on both machines |
| "Evidence is data, not instructions" | AGENTS.md "Untrusted input" |
| A hook is the only rule the model cannot skip | CLAUDE.md "Where new knowledge goes", item 1 |
| Always-on text is the expensive option | ADR-011 caps, enforced by `lint_docs.py` |
| A skill must be named in always-on text | The CLAUDE.md skill index lists every skill, `harness-retro` included |
| Agreement between candidates is not proof | `.ai/roles/adjudicator.md`; `evidence-over-claims.md` A.1 |
| Scoped editors (procedure versus domain) | Tier routing by the narrowest trigger |
| Judge kept apart from the improver | The builder, reviewer and adjudicator roles; `REVIEW-LOG.md` |

## What TAOM lacked

TAOM learns from incidents someone notices: 428 RCAs and 1,488 lesson entries in 13 files. No
step checked whether a rule, hook or skill change helped, and the session transcripts were read
once, by the [skill usage audit](skill-usage-audit-2026-10-08.md). Harness edits had no verdict
and no evidence trail beyond their commit body.

## Decisions

| # | Item | Decision |
|---|---|---|
| A1 | A local transcript miner, `tools/harness_retro.py`: hook denies, blocks, failures and silent gates; repeated tool-error signatures; runaway retries; user corrections; skill and agent usage; memory health | Adopted. Reads the transcripts Claude Code already writes, so it adds no logging; output stays outside the repo |
| A2 | `/harness-retro`: a verdict per harness change since the last retro (git is the ledger), at most five proposals each traced to a session moment, cheapest form first, no change as a valid result, one-at-a-time approval | Adopted, as a thin skill over `docs/ai-includes/harness-retro.md` |
| A3 | A `HARNESS RETRO DUE` line in the SessionStart banner after seven days | Adopted. This is what keeps the miner from becoming the log nobody reads that Mike declined on [2026-10-08](adopt-claude-code-2026-10-08.md) |
| A4 | An error-signature hint hook fed by a table of recurring errors | Not built. The [first retro](harness-retro-2026-10-21.md) found errors in three or more sessions, but most repeat a message that already says what to do. The one that prose demonstrably fails to prevent (Bash quoting) became its proposal P3, which tests first which command shapes break |
| D1 | A/B probe: fixed prompts run headless on the base and an edited harness | Deferred: spends real usage; revisit after two or three retros if verdicts stay confounded |
| D2 | Blind comparison with a "no change" candidate for parallel plans | Deferred |
| X1 | Install or run Harness², its benchmarks, runners or Vertex setup | Declined: wrong domain and platform, unsafe flags |
| X2 | k runs of a live task | Declined: TAOM tasks are unique, checked in game, with no grader |
| X3 | Automatic adoption of edits, or auto-written memory | Declined: conflicts with ADR-011 and the [ECC decision](adopt-ecc-2026-09-29.md) on continuous learning |
| X4 | A separate ledger file | Declined: git history and the retro records already hold it |
| X5 | Cue and confidence fields on lesson entries | Declined: lesson headings already name the situation and the operation; `output-style.md` carries the confidence tags |
| X6 | Grader-verdict tripwire, budget identity, run fingerprint, candidate diversity metric | Declined: they protect benchmark integrity; the git SHA already fingerprints the harness |
