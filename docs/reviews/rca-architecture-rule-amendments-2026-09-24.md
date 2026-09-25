# RCA: plan 021, architecture rule amendments (2026-09-24)

## Top-line

Branch `improve/021-architecture-rule-amendments`, diff `bec0389d..7d1b7a54`, reviewed by four
`/deep-review` lenses (standards, completeness, data flow, design) and one Codex adversarial pass
(gpt-6-astra, ultra). The change is documentation only: no C#, XML or hook file, and the full suite
is unchanged. Its stated goal was that "every rule source says the same thing". **It did not reach
that goal**: the amended specific rules and an always-loaded general rule disagreed (Codex P2 1),
two ADRs disagreed until the orchestrator's `7d1b7a54` (Codex P2 2 and 3), and several other
statements of the old rules survived because the plan's leftover sweep searched for one spelling of
one part. The ADR's cited exemplars also break two of its own new conditions. Report:
`docs/reviews/deep-review-021-architecture-rule-amendments-2026-09-24.md`.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| C1 | MEDIUM | `think-before-coding.md:33-34` allowed an interface only for an adapter, a fake or a second implementation, forbidding the narrow hook interface that AGENTS.md and three rule files allow; the named example `IOnPartyUpgradeResourceCheck` has one implementer and no test | Rule self-contradiction | The plan wrote the general rule (Step for part B) and the hook permission (part C) as separate steps with separate phrase checks; nobody read the general rule against the new permitted case | Fixed. New lesson in `lessons/misc.md` |
| Codex 2 | MEDIUM | ADR-008's exception allowed only "a static read" while ADR-007 condition 2 allows the calls for one action; `RefugeService.ChargePlayer` mutates gold | Rule self-contradiction | Step 0 wording drafted before decision 49 chose the looser seam variant, and not re-read after it | Fixed by the orchestrator in `7d1b7a54`. Same lesson |
| Codex 3 | LOW | ADR-002 migration step 4 still created a service interface every time | Incomplete sweep | The plan listed the guideline and checklist lines to edit, not the procedure that applies them | Fixed in `7d1b7a54`. Recurrence note on the misc "stale-claim grep is a floor" lesson |
| C2 | LOW | Standards lens said "One exception", omitting ADR-007's value-type exception | Rule self-contradiction | The line was rewritten to add the seam and fixed a count that the ADR's own Exceptions section already exceeded | Fixed. Same lesson as C1 |
| C3 | LOW | `submodule-lifecycle-and-harmony.md:45,:50` kept the mandatory `→ IHook →` one-liner | Incomplete sweep | Step 8.6 grepped `IHookInterface` and "hook interface → service", not `IHook` | Fixed. Recurrence note |
| C4 | LOW | `decompiled-code-analysis.md` Phase 4 always created both interfaces while `:99` of the same file made the hook conditional | Incomplete sweep | The plan edited the file's diagram and question 7, not its procedure | Fixed. Recurrence note |
| C5 | LOW | `audit-playbook.md:68` and `codex-verify/SKILL.md:50` still graded any sealed type in a service as ADR-007 | Incomplete sweep | Step 8.6 swept only for part B and part C wording; nothing swept for part A | Fixed. Recurrence note |
| C6 | LOW | `review-reference.md:21`, rewritten by this diff, still said "(v1.5.2)" | Stale version | The executor rewrote the clause it was told to and kept the rest of the line; `lint_docs` stale_versions did not match this form | Fixed (points to the pin file) |
| C7 | LOW | `architecture.md:112` "mocked adapters" only, beside the new seam bullet | Incomplete sweep | Same as C4 | Fixed |
| C8 | LOW | CHANGELOG credited "Mike's commit" and omitted `7d1b7a54` | Wrong provenance | The entry was written before the Codex round and from the plan's description of Step 0, not from `git log` | Fixed |
| C9 | LOW | `csharp-patterns.md:13` put an uncomputed count ("three patches") in a path-scoped rule | Tier rule | The plan prescribed the sentence | Fixed |
| O1 | MEDIUM | ADR-007 limits the exception to `protected virtual` members, but the four named services factor seam bodies into private helpers returning sealed types (`RefugeService.FindParty`, `FindHero`, `FindTroop`, `PinPartyAi`; `SupplyOrderService.FindHero`; `WardenService.FindHero`) | Rule versus its exemplars | The Step 0 note that offered the looser variant checked only `RefugeService`'s seams, and only the seams, not what they call | NEEDS MIKE (ADR wording listed in the report). New lesson in `lessons/misc.md` |
| O2 | MEDIUM | Condition 2 forbids TAOM decision logic in a seam, yet `SupplyOrderService.ChargePlayer`, `RefugeService.FindNearestHostile`, `WardenService.MintCompanionFromTroop` and `CompanionsInMainParty` carry it | Rule versus its exemplars | Same as O1 | NEEDS MIKE. Same lesson |
| O3 | LOW | ADR-007:28 lists `CampaignTime` as a sealed class requiring an adapter; condition 1 allows it as a struct | Rule self-contradiction | The new subsection was written without re-reading the section it qualifies | Orchestrator ADR edit. Same lesson as C1 |
| O5 | LOW | ADR-008:232 and :263 still forbid what ADR-008:15 now allows | Rule self-contradiction | Same as O3 | Orchestrator ADR edit |
| O6 | LOW | ADR-002:303 checklist narrower than guideline 7 | Rule self-contradiction | The plan edited the neighbouring checklist line only | Orchestrator ADR edit |

## Root-cause pattern

Two themes cover every finding.

1. **An amendment adds a permitted case to a specific rule and leaves the general rule, the sibling
   ADR and the checklists absolute** (C1, Codex 2, C2, O3, O5, O6). Each statement was edited to its
   own prescribed sentence and checked by a substring gate, which proves the sentence is present,
   never that two sentences agree. Codex's suspect 9 names this exactly.
2. **The leftover sweep searched for one spelling of the old rule, and for two of its three parts**
   (Codex 3, C3, C4, C5, C7). This repeats the plan 014 lesson in `lessons/misc.md` ("A plan's
   stale-claim grep is a floor"), now with a recurrence note.

O1 and O2 are a third, smaller theme: an ADR that cites code as its exemplar was not checked
against every condition it states, for every service it names.

## Why each agent missed these

The plan's own executor and the orchestrator's Step 0 are the implementation side; all four lenses
and Codex ran afterwards and between them found every item above.

- **Agent 1 (Standards):** found C1, C2, C3 (as a follow-up), C6, C7, C8, O1, O3, O4, O5, O6;
  missed C4, C5, C9 and O2 because its harness checks (H1 to H6) read the changed lines and their
  budget, not unchanged files that restate the rule or the seam bodies behind the cited services.
- **Agent 4 (Completeness):** found C1, C3, C5, C6, C7, C8, O3, O5, O6; missed C2, C4, O1 and O2,
  which need the ADR's Exceptions section and the seam bodies read against each condition.
- **Agent 5 (Data flow):** found all but C6 and C9; its consumer trace is what reached the audit
  prompts (C5) and the Phase 4 procedure (C4).
- **Agent 6 (Design):** found C1, C9 and the "engine" wording; it reviews changed units, so the
  sweep gaps were outside its lens.
- **Codex:** found C1 and the two ADR items; it compared the sentences the plan prescribed and did
  not sweep the repo for other statements of the rules (see the report's "Things Codex missed").

## Feedback memories to codify

None beyond the lessons entries: the three themes are covered by `lessons/misc.md` (a new lesson
for the first, a recurrence note for the second, a new lesson for the third). The AGENTS.md "Lessons From Prior Reviews" items are
listed in the report under "AGENTS.md lessons (pending)".
