# RCA file and lessons entry

Step 3e of `.claude/skills/deep-review/SKILL.md` sends you here before you write an RCA file or a lessons entry.

## The RCA file

Record each finding that needs an RCA file this way, once you have confirmed it, then write the file in the format below.

1. Write the finding text + severity.
2. **Why missed:** what assumption, scope gap, or pattern blindness let it through? Be specific: name the rule that should have caught it, name the file/line that exhibits the pattern.
3. **Preventive action:** generalizable rule, or scope extension to an existing rule? Or one-off?
4. If the pattern has shipped before (grep `docs/reviews/rca-*.md` for it), call that out: repeat-offender bugs need stronger preventive action than first-time bugs.

**Write the result to `docs/reviews/rca-<feature>-<YYYY-MM-DD>.md`** following the format of `docs/reviews/rca-quickactions-2026-05-06.md` or `docs/reviews/rca-scene-scripts-cs-road-2026-05-13.md`:
- Top-line summary
- Findings table: # | Sev | Bug | Category | Why Missed | Preventive Action
- Root-cause pattern section (if 2+ findings share a theme)
- "Why each agent missed these" section: for each deep-review agent that ran and didn't catch the finding, state why their rule set didn't apply or why the agent's scope was too narrow
- "Feedback memories to codify" section, only if there's a genuine systemic pattern; don't manufacture rules

**A deferred finding needs an owner.** Every uppercase `FOLLOW-UP` line you write in an RCA cites an issue (`#<number>`) or says `no issue:` and why; `python tools/lint_docs.py --fail-on-drift` fails on a new line that does neither (#984), and the commit hook runs the same check on the staged RCA. To mention the marker in passing, put it in backticks (`` `FOLLOW-UP` ``); an issue number in backticks does not count as a citation.

## The lessons entry

Every systemic finding gets one, with or without an RCA file.

**Append the durable lesson to the master record.** For each systemic finding, also add an entry to the matching category file under `docs/reviews/lessons/` (e.g. `lessons/gamemodels-services.md`, `lessons/harmony-il.md`; index: `docs/reviews/LESSONS-LEARNED.md`), in the house shape (`### <imperative rule>` → `**Why missed:**` → `**Prevent:**` → `**Source:**`). The per-feature `rca-*.md` is the incident report; the lessons entry is the cross-feature rule that stops the *category* from recurring: it is the canonical, always-consulted record. Read the relevant category FILE before the next review of that subsystem (per-category files keep the read cheap). Only touch `MEMORY.md` if a new feature/topic memory file is involved.
