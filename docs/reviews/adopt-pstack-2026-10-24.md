# pstack adoption review (2026-10-24)

Review of [backnotprop/pstack](https://github.com/backnotprop/pstack) (MIT, v0.15.32, a mirror of
Cursor's `cursor/plugins/pstack`) against TAOM's harness, through `/adopt-external`. Mike asked for
ways to improve TAOM's processes, code, workflows and docs, and then asked that each improvement fire
on its own inside the workflows rather than wait to be remembered.

## Coverage

- **Repository:** `18b6c36`, about 210 files, read through the GitHub contents API and a shallow
  clone in the session scratchpad. Nothing was installed or run. Three read-only readers covered
  `poteto-mode` and its 23 playbooks; the 24 `principle-*` skills and the writing skills; and the
  review, verification and meta skills plus the `benny` automation pack. Every claim this record
  relies on was re-checked against the TAOM files.
- **Not read:** `skills/statechart/scripts/` (TLA+ tooling, out of domain), the `why` source
  playbooks, and a few reviewer prompt references.

## Security

- **Safe to learn from:** yes. No hooks, no install lifecycle scripts, no telemetry, no credential
  reads. `python tools/audit_claude_config.py --root <clone> --external`: 202 files scanned, exit 0,
  one INFO (YARA not installed).
- **Safe to install:** no.
  - `skills/poteto-mode/scripts/bootstrap.ts` runs `bun install` on first use, with unpinned
    `latest` dev dependencies.
  - `setup-pstack` writes an always-load rule into `~/.claude/rules/`.
  - `worktree-audit.sh` runs `git fetch`, calls `gh` with stored credentials, and runs any binary
    named `origin` that is on PATH.
  - The `/loop` playbooks re-read skill text from trunk at every tick, so remote text is trusted
    again on each cycle.

## Verdict

About 85% duplicates TAOM or is out of domain. pstack targets TypeScript web apps, PR stacks, Bugbot
and Cursor Automations. TAOM already has the core of it in `evidence-over-claims.md`,
`simplicity-criterion.md`, `/investigate`, `/deep-review`, `/harness-retro` and the "Where new
knowledge goes" ladder. Seven gaps were real.

## Adopted, and what fires each one

| # | Item (pstack source) | TAOM change | Trigger |
|---|---|---|---|
| A1 | Two-edit test check (`principle-test-behavior-not-implementation`) | `deep-review/lenses/4-completeness.md` item 9; pointer in `rules/tests.md` | `/deep-review`, which `check-deep-review.sh` requires before a C# or XML commit; the rule loads with any test file |
| A2 | Comment review (`no-comments`, `comment-sicko`) | `deep-review/lenses/1-standards.md` item 9a; `code-quality.md` example aligned with ADR-009 | `/deep-review`, as A1 |
| A3 | Verification recipe per feature (`create-verification-skill`) | `## How to verify in game` in `docs/features/TEMPLATE.md`; `/ship` step 9; `completion-workflow.md`; Lens 7 OWED format | `lint_docs.py` drift check: a NEW feature doc without the heading blocks its commit through `check-doc-config-drift.sh` |
| A4 | Premise test and legitimate-state test (`principle-attack-the-premise`, `principle-fix-root-causes`) | `/investigate` 3-strike rule and Phase 5 step 1 | `/investigate`, which CLAUDE.md requires for every crash |
| A5 | Worktree audit (`worktree-audit.sh`, idea only) | `tools/worktree_audit.py`, read-only, with tests | SessionStart banner above 15 worktrees; `/ship` step 11; `/improve` cleanup step |
| A6 | Perf method (`perf-issue`, `hillclimb`, `benchmark-checklist`) | `docs/ai-includes/perf-method.md` | `perf_runs.py compare` prints `INCONCLUSIVE` and links the method when a group has fewer than 5 runs |
| A7 | More prose tells (`unslop` rules 26, 27, 29, 32, 33) | One paragraph in `rules/output-style.md` Part 2 | Always loaded |

A1, A2 and A4 needed no new hook, because the skill they live in is already required. A3, A5 and A6
are machine checks, the first rung of the CLAUDE.md ladder. `/harness-retro` gives each of them a
verdict at its next run.

## Review follow-ups, done 2026-10-26

- An unresolvable `--dash-base` stops `lint_docs.py` with exit 2 before any check runs, instead of
  surfacing later as a gating `git-diff-failed` finding.
- An amended commit is checked against `HEAD~1` (the empty tree for a root commit); scratch-repo
  tests prove a feature doc added in the amended commit is gated.
- `perf_runs.py compare` also calls a result inconclusive when its rows span more than one scene,
  or when a metric has fewer than 5 values in a group.

## Offers, decided 2026-10-26

Mike asked for the recommendation on each offer to be carried out.

| Offer | Decision | Reason |
|---|---|---|
| `/deep-review`: "no findings is valid"; trace the call site before a hypothetical finding counts (`interrogate/references/lead-judgment.md`) | Built, the second half only | `deep-reviewer.md` already said an empty report is valid; it now says a finding that needs a particular input or state names the call site, else it is hypothetical, LOW at most |
| `/harness-retro`: a rule broken again gets an enforcer, not more text (`skills/correct`) | Built | One bullet in `docs/ai-includes/harness-retro.md` phase 4 |
| `think-before-coding.md`: a fact a quick run can show is not the user's question | Built | Three words in the existing "Don't ask" sentence; the always-load rules sit at about 16,280 of 16,384 bytes |
| `plan-lint` for `plans/` (`scripts/check-plan.mjs`) | Rejected | All 41 numbered plans already carry Status, Drift check, STOP, Out of scope and Executor instructions; the cold review in `/improve` holds the template |
| Owed-work digest (`skills/ketchup`) | Rejected | Duplicate: FOR-MIKE.md opens with `## Waiting on you`, one line per item with the recommended answer and the evidence (`improve/references/run-protocol.md`) |
| `evidence-over-claims.md`: name the test that failed before a bug fix | Rejected | `tests.md` (RED first) and the two-edit test check cover it, and the always-load budget has no room |

## Declined

| Item | Reason |
|---|---|
| `poteto-mode` router, `poteto-agent` | The CLAUDE.md skill table already routes |
| autopilot, shipping, babysit, opening-a-pr, Bugbot triage | PR-stack machinery; TAOM commits to trunk |
| arena, swarm | Paid fan-out; `agent-teams.md` covers parallel work |
| statechart, formal verification skills | TLA+, out of domain |
| benny | Slack and Cursor Automations |
| setup-pstack, automate-me, poteto-help | Model routing exists (`model-effort-selection.md`) |
| recall, how, teach, why | Memory, `docs/INDEX.md` and the confidence tags cover them |
| bro, sharpen, in-your-own-words | Prompt tools for the user |
| typescript-best-practices, make-bot-ui | Wrong stack |
| `outcome-oriented-execution` | Conflicts with ADR-004 (every step stays green) |
| The `tdd` skip clause | Conflicts with "Test first, always" |
| The plugin itself | Never installed (see Security) |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/external-repo-adoption.md](../ai-includes/external-repo-adoption.md)
- [docs/reviews/adopt-claude-plugins-official-2026-10-26.md](./adopt-claude-plugins-official-2026-10-26.md)
- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)

<!-- backlinks-end -->
