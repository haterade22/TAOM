# Harness retro

The recurring check on TAOM's own AI harness: hooks, rules, skills, agent definitions, CLAUDE.md
and AGENTS.md. It is the executable procedure behind `/harness-retro`. It adapts the evidence
discipline of Google's Harness² ([review](../reviews/adopt-harness2-2026-10-21.md)): a harness
change earns its place by what the sessions show, and a change that shows nothing is a candidate
for deletion. Harness² found that edits made without evidence made the harness worse.

## When

- The SessionStart banner prints `HARNESS RETRO DUE` (seven or more days since the newest
  `docs/reviews/harness-retro-*.md`, or none exists). The banner tells the session to say so to Mike
  once, in its first reply, and ask whether to run `/harness-retro`; do not start one unasked. A
  short record with no proposals silences the banner for another seven days.
- Mike asks what the harness keeps getting wrong.

A retro is advisory work. It edits no harness file until Mike approves a proposal, and git,
issues and paid calls keep their usual rules.

## Phases

### 1. Collect the evidence

Run `python tools/harness_retro.py`. It reads this project's Claude Code transcripts (main sessions
and subagents) since the last retro and writes `report.md` and `signals.json` outside the repo.
It makes no model calls. Sections: sessions, hook outcomes (denies, blocks, broken hook runs,
cancelled and timed-out runs, gates with no fires, ask-only gates whose fires leave no trace),
permission outcomes, tool-error signatures (RECURRING at three or more sessions), runaway retries
(three or more assistant messages, not parallel calls of one), user corrections, skill and slash
command usage, subagent usage, memory health, and tool output size (section 9: characters of
results by tool and by shell command family, the baseline for any output-trimming change).
Section 9 also compares each family's share of all output with the newest earlier run beside
the report folder (dated folders under one root by default) and lists every family that grew by
2 points or more; the console line `output growth:` says how many. Its "Top files read" table
does the same for the Read tool, about half of all output: each file's reads, whole reads and
share of Read output, folded across worktrees, with a `file growth:` line for files that grew by
2 points of Read output (#1002).

Section 10 (tokens and cache) reads `message.usage` of every request once (deduplicated by request
id, so a request copied into a resumed session is not counted twice) and reports the totals, the
cache-read share of input, the top five skills (main-session tokens after the skill was invoked) and
subagent types by total tokens, and the count of cache breaks (one request writing more than 100,000
tokens to the cache). The skill and subagent tables also split out output and cache-write tokens
(the total is mostly cache reads), and signals.json keeps all four fields. It is for spotting where
cost goes and where the prompt cache breaks. A user record the transcript marks human (`origin.kind`
or `turnOrigin` is "human") counts as a human turn even when it opens with an injected block such as
`<ide_opened_file>`; its typed text after those blocks is what the correction detector reads. Output
tokens are a floor (many subagent requests are written only mid-stream, and the report says how
many). It is not a verdict by itself, and a skill's total grows with the work it was given.

The same requests are also split by agent type and model (`main` or the subagent type from its
`.meta.json`; no prices), with the five costliest transcripts and the model overrides (a
definition in `.claude/agents/` sets `model:` but the requests ran on another family). Each
agent's share of cache creation plus output is compared with the previous run: the console lines
`token growth:` (5 points or more) and `model overrides:` (#1008).

Section 11 prices the usage at list rates (a proxy for relative size): by agent, by main-session context size, cache rebuilds by idle gap, and review runs. Compare it with the last retro's numbers by hand.

Section 12 lists the files two sessions edited: every Edit, Write, MultiEdit and NotebookEdit call
(a subagent's counts for its main session), with a file shared when two sessions edited it within
24 hours of each other. Files under the user's `.claude` folder (the memory index, plans, user
settings) are left out and counted in `excluded_claude_home`; a checkout's own `.claude` folder stays. It gives the shared files, the session pairs and the top
10 files with their sessions and first and last edit; the console line is `shared edits:`. Section 13 counts the
plain tool errors (section 4's kind, not hook denies) among up to 20 tool results before and 20
after each compaction of a main transcript, each window stopping at the neighbouring compaction, with both rates and the top 5 signatures after; the
console line is `post-compaction:`. Both came with #1138.

Three more report sections feed the verdicts in phase 3. Section 1 lists the Claude Code versions seen: compare them with the `since vX` notes in `.claude/rules/harness-*.md`, because a version past a fact's last check may have changed it. Section 7 pairs each SessionStart banner with the skill it names (sessions that printed it, invocations, sessions that ran the skill's command in a shell), so a banner nobody acts on shows, and its "Rule files loaded" table counts the sessions and subagent transcripts that listed each rule, so a rule that never loads shows too. `--exclude-session ID` (repeatable) leaves a session out when its work would distort a comparison.

Also run `python tools/analyze_reviews.py --summary --no-plot` for the reviewer accuracy that
`REVIEW-LOG.md` already tracks (`--no-plot` leaves the committed `progress.png` alone).

If the miner exits 3 with UNCHECKED, stop: no evidence means no retro, not a clean one. An
`output growth: UNCHECKED` line on a normal run only means no earlier run was found to compare
with; continue, and say so in the record.

### 2. List the harness changes since the last retro

```bash
git log --since <last-retro-date> --format='%h %cs %s' -- .claude AGENTS.md CLAUDE.md .ai docs/ai-includes docs/adrs
```

Add the preventive actions of the RCAs written in the same window (`docs/reviews/rca-*.md`): each
one is a harness change with a stated purpose. Git is the ledger; there is no separate one.

### 3. Give each change a verdict

One row per change older than seven days: the component, the change (commit), what was observed
(a signal from the report with its session ids), and the verdict.

| Verdict | Use when |
|---|---|
| helped | The signal the change targets moved the right way, in more than one session |
| hurt | It added denies, errors, corrections or retries that were not there before |
| no observed effect | The window holds the situation it targets, and nothing changed |
| confounded | One commit changed several components, or one session is all the evidence. Prefer this to a guessed attribution: a wrong "helped" misleads every later retro |
| too early | The window does not yet hold the situation it targets |

A change meant to trim what sessions read (an output filter, a quieter flag, a rule about how
much to read) takes its verdict from section 9: the family it targets must shrink against the
growth baseline, or it is "no observed effect".

A gate that guards a rare, costly event (force-push, `--no-verify`, a dangerous git form) is kept
with zero fires; record it as a guard, not as dead. A change with "no observed effect" in two
retros becomes a deletion proposal: a deletion that holds parity is always kept
([simplicity-criterion.md](../../.claude/rules/simplicity-criterion.md)).

### 4. Propose, at most five

Each proposal names its evidence: the session id and the moment (a correction, a deny, a recurring
error, a runaway). A proposal that cannot name a moment is speculation; drop it.

- **Priority:** a divergence first (the agent did it right in some sessions and wrong in others,
  so it can do it and the harness leaves it to chance), then a failure repeated across sessions,
  then a risk seen but not yet realised.
- **Output growth:** each row of section 9's growth tables (shell families and files) is a
  candidate, and so is a file at the top of "Top files read" that every session reads whole, such
  as a review lens or a path rule. Name the sessions behind the row (the report's family key is
  the command, the file key its path in the repo), and propose only when the cause is a
  harness gap, such as a whole file read where a range would do; growth from more work is not a
  defect.
- **Token growth:** a section 10 token growth row or a model override is a candidate. Name the sessions
  behind it; propose only when the cause is a harness gap (a definition that does not pin its
  model, a lens that re-sends a large prompt), since more review work also raises a share.
- **A rule broken again:** when the moment breaks a rule that is already written, more text is not a
  proposal. Propose the enforcer (a gate, a test, a check in a skill step that runs), or name why
  none can exist.
- **Form, cheapest first:** a gate or hook (it acts in the tool loop and costs no context), then a
  script in `tools/`, then a skill or path rule (loaded only when needed), then always-on text in
  AGENTS.md or CLAUDE.md last. A proposal that adds always-on text names what it cuts. Route by
  CLAUDE.md "Where new knowledge goes".
- **No change is a valid result.** An edit the evidence does not support costs context and buys
  nothing.

### 5. Present the proposals

One at a time: what, pros, cons, recommendation (Mike's standing preference). A long list goes on
an HTML page that takes batch answers. A proposal is applied only on his approval, through the
normal tiers, tests and commit rules.

### 6. Write the record

`docs/reviews/harness-retro-<YYYY-MM-DD>.md`, dated the day of the retro. Its date is the next
retro's start, and it silences the banner. Contents:

- **Window and coverage:** dates, session counts, parse failures.
- **Signals:** the aggregate tables that bear on a verdict or proposal, with session ids, and
  always section 9's top families and growth table: the report folder is outside the repo and can
  be cleaned, so the record is the durable baseline.
- **Verdicts:** the phase 3 rows.
- **Proposals:** each with its evidence and Mike's decision.

Never copy transcript text into the record. Cite a session id and timestamp, and paraphrase what
happened in a few words. The miner redacts secrets in its own output, but the repo is public and
the transcripts hold Mike's words.

## Pitfalls

- The transcript format is internal to Claude Code and changes between versions. A drop in a
  signal to zero after an update is a parser question before it is a harness win: check the
  miner's parse failures and its record counts first.
- Counts measure how often something happened, not why. Read the session around a moment before
  you build a proposal on it.
- A hook deny is usually the harness working. It is evidence of a problem only when the same deny
  repeats in one session (the agent did not learn from the reason) or the reason was wrong.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/harness-retro-2026-10-21.md](../reviews/harness-retro-2026-10-21.md)

<!-- backlinks-end -->
