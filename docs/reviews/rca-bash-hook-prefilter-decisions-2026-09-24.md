# RCA: plan 013 maintainer decisions D39 and D40, review (2026-09-24)

## Top-line

The follow-up that applied D39 (each gate prefilters on its own word) and D40 (a payload holding
any JSON `\u` escape takes the full parse) is correct: six lenses and Codex found no changed
gate decision beyond the eight intended D40 cases. Every confirmed finding sits in the evidence
around the change. The CHANGELOG's before-case was taken from the RED run of an intermediate
tree (D39 applied, D40 not yet), where `git \u0063ommit` did slip through, and presented as the
behaviour of the committed base, which denied it. Two new 4c rows looked like coverage but could
not fail on the mutant they exist for: the commit gates had no `git -C <dir> commit` row, so a
filter narrowed to `git commit` stayed green, and the default escaped row kept a literal `git`,
so a `git`-filtered hook without the escape arm passed it. The rest are wording: coverage, savings
and safety reasoning stated more broadly than the code or the tests support.

All fixed in `fix(hooks): v2.0.30 - decision review follow-ups for plan 013`. R3 and R4 have a
failing proof first: with two planted mutants, the committed suite caught neither gap and the new
suite catches both. The others are text fixes whose evidence is the probe or count cited.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| R1 | LOW | CHANGELOG before-case `git \u0063ommit -m "no label here"` passed only in the D39-only intermediate; the base denied it (`\u0067it commit` was the real hole) | Evidence from the wrong revision | The example was lifted from `red-d40.txt`, whose tree had D39 but not D40; nobody ran it on `5dcef67a` | Lesson "Prove a before-case against the committed base, not the RED intermediate" |
| R2 | LOW | "240 payload cases found no changed decision" in the entry that documents a changed decision; the parity script held no escaped payload and compared stdout and exit code only | Claim wider than its evidence | The first review's parity sentence was reused after D40 changed what parity could show | Same lesson: restate a reused claim against the new change |
| R3 | LOW | Commit gates' comment said they need `git commit`; 4c had no `git -C <dir> commit` trigger row, so a filter narrowed to `git commit` stayed green | Filter arm without a test row | Rows were written for the word the filter reads, not for each arm of the hook's trigger (`*"git commit"* \| *"git -"*" commit"*`) | Row added, proven by a mutant. Lesson "Build a coverage row that fails on the mutant it exists for". Repeat of "give every alternative of a filter its own trigger row" (same file) |
| R4 | LOW | 4c's default escaped row `git \u0063ommit` kept a literal `git`; a `git`-filtered hook lacking the escape arm passed it | Test row passes through another arm | The row was right for the six commit gates it was written for; the `*)` default also serves the confirm gates' kind and any new hook, which nobody checked | Row now holds neither word, proven by a mutant. Same lesson as R3 |
| R5 | LOW | "each blocking gate" for 4d, which covers five of ten | Coverage claim not counted | Written from intent; the first review record said "five" correctly | Wording fixed. Same lesson as R1 (count before claiming) |
| R6 | LOW | Catalog still said the raw test rests on how Claude Code writes the payload; after D40 it rests on JSON grammar | Superseded reasoning left in place | D40 was added as one more arm; the paragraph arguing the old premise was edited around, not re-derived | Paragraph rewritten; lesson "When a fix removes a premise, retire it from the argument" |
| R7 | LOW | "`git status`, `git diff` and `git log` start no Python" ignores the description field | Savings claim ignores part of the input | The raw test reads the whole payload; the claim reasoned about the command only | Wording fixed; one-off |
| R8 | LOW | D39's text named `suggest-compact.sh`; leaving it out was sound but unrecorded | Decision deviation unrecorded | The builder judged it covered by D42 and paraphrased D39 without it | Record annotated for Mike; one-off |
| R9 | NIT | "Fail open on escapes" for an arm that forces the check | House term used with the opposite meaning | The phrase described the prefilter "opening", not the house meaning (allow on failure) | Reworded; one-off |
| R10 | NIT | First review record: two gates silent on the old section 5 payload; seven were | Count not re-run | Counted from the two failures seen first, not from a run | Fixed; covered by the R1 lesson |
| R11 | NIT | "Visible change" for stderr from an exit-0 hook | Visibility assumed | `harness-facts.md` Visibility row not consulted | Fixed; one-off |
| R12 | LOW | #661 filed after the commit; the record said "not filed yet" | Stale record | Timing: the issue was filed 12 minutes later | Annotated; the citation waits for batch 3, per `PROGRESS.md:68` |
| R13 | LOW | #661 body carries R1's claim and `\^[` for `\u001b` | Evidence from the wrong revision; escape text mangled in transit | The issue draft reused the CHANGELOG text; the escape was probably decoded to an ESC byte on the way and printed as `^[` (UNVERIFIED) | Needs Mike (public issue). Covered by the R1 lesson and the existing V1 note on the Edit tool |

## Root-cause pattern

R1, R2, R5 and R10 share one theme: **a claim about behaviour or coverage was written from the
run or the intent nearest to hand, not from a run on the state it describes.** R3 and R4 share
another: **a coverage row was checked for passing on correct code, never for failing on the
mutant it exists to catch.** The first review of plan 013 recorded the same second theme ("prove
each row by deleting the arm it covers"); this follow-up added rows without doing it.

## Why each agent missed these

The builder is the author; the lenses below found every item, so "missed" here means which lens
did not report a finding another lens did.

- **Standards:** found R1, R3, R5, R6, R9, R10. It did not see R4: it ran the suite and checked
  row coverage per arm of the hook trigger, not per arm of the prefilter.
- **Efficiency:** found R7 and R14. It is scoped to cost; the CHANGELOG and test-row defects are
  outside its rules.
- **Completeness:** found R1, R2 (part), R4, R5, R6, R8, R10, R12, R13. It did not see R3: its
  check is that tests exist in both directions, not that each trigger arm has a row.
- **Data flow:** found R1, R2, R4 (as G1), R5, R6, R8, R9, R10, R11, R14. It did not see R3: it
  traced `git -C` through the hooks (C1 to C6) and found the flow connected, which it is; the gap
  is only in the tests.
- **Design:** found R9 and noted R1 and R7. Its lens asks for a simpler or better design, not for
  test adequacy.
- **Tooling:** found R1, R2, R4, R5, R7, R9, R10. It did not see R3: it mutated the escape arm
  of one hook, not the word arm, so the missing `git -C` row never came up.
- **Codex:** found R1 and R5. It read git refs only and ran no mutant, so it could not see a row
  that passes through another arm.

## Feedback memories to codify

None beyond the lessons in `docs/reviews/lessons/build-tooling-workflow.md`: the patterns are
already rules there (mutant-prove each row; evidence before the doc) and need sharper wording, not
a new memory.
