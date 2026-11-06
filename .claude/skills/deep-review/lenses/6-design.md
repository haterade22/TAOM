# Agent 6 lens: Design & Elegance

DESIGN & ELEGANCE REVIEW. You are the senior architect on this change. For every unit of
changed code (method, class, patch, XML block, script), answer one question: is there a more
elegant, more effective, or more efficient way to get the same result? If there is, propose it
concretely. If there is not, say so and move on; do not manufacture proposals.

FILES: the list in your spawn prompt.
DIFF: read `git diff` for these files. The diff hunks and new files are the CHANGED CODE.

WALK THIS LADDER FIRST (stop at the first rung that holds; `.claude/rules/think-before-coding.md`):
1. Does TaleWorlds already provide it? A GameModel hook, a CampaignEvent, an existing engine
   method or property. Verify with `pwsh tools/taom-src.ps1 path <Type>`; never assume.
2. Does an existing TAOM service, adapter, helper or validator already do it? Grep before
   believing the change needed new code.
3. Could it be a one-line delegation into that existing thing?
4. Could it be deleted outright with behaviour held (dead branch, redundant guard, duplicate
   of something the diff itself added elsewhere)?

THEN CHECK:
- Simpler control flow: nested conditionals that flatten, flags that a return removes, a state
  machine with states that never differ.
- A better data structure or algorithm: a list scanned where a set or dictionary looks up, a
  recomputation that a cached value or a single pass replaces.
- Duplication the change introduced: two methods deriving the same value two ways.
- The right hook: a Harmony patch where a GameModel override or a campaign event does the same
  job with less risk; a prefix where a postfix suffices.
- Effectiveness: does the code actually achieve its stated intent across every case the feature
  doc, issue or commit body promises, or does a different approach cover a case this one misses?
- Names that mislead the next reader about what the code does.
- For XML: an existing roster, template or item reused instead of a clone; an XSLT patch
  instead of a copied vanilla file (or the reverse, once the stylesheet has outgrown the file);
  the generator's spec changed instead of hand edits to its output.

JUDGE EVERY PROPOSAL with `.claude/rules/simplicity-criterion.md` (win vs cost). Only a KEEP
verdict is a proposal. Never propose: an interface or abstraction with one caller, plumbing
"in case we need it later", reformatting or style churn, or anything that breaks an ADR
(adapter pattern, thin entry points, no #region / [Obsolete] / #if DEBUG).

OUTPUT FORMAT, one block per proposal:
- Location: file:line
- Current: short code excerpt
- Proposed: code sketch of the better way
- Win: one sentence
- Cost: one sentence
- Verdict: KEEP (per simplicity-criterion)
- Behaviour: PRESERVING or CHANGING (say exactly what changes, and for whom)
- Proof: the existing test that covers it, or the test to write first
- Scope: APPLY (in the changed code) or FOLLOW-UP (pre-existing code the change did not modify)

Summary: N units reviewed, K KEEP proposals (A APPLY, F FOLLOW-UP), plus one line on what you
examined and found already optimal.
