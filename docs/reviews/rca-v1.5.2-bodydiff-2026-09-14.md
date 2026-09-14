# RCA: the v1.5.2 body diff (member-level drift under unchanged signatures)

**Date:** 2026-09-14 · **Branch:** `bannerlord-1.5.x` · **Scope:** the body-level pass over every
engine member TAOM binds, run after the compile commit `8b9f0a23` had every signature gate green.

## Top line

The compile-level gates (build, `HarmonyPatchBindingTests`, `snapshot_api_surface.ps1`) answer one
question: does every bound member still resolve with the signature TAOM expects. They say nothing
about a member whose signature is unchanged and whose body now does something else. A member-level
body diff of v1.4.8 against v1.5.2 (`bodydiff.py`, 62 rows: 29 GameModel base methods, 28 patch
targets, 4 loaders, 1 reflection site) put 51 changed bodies in front of six review agents. Result:
one abstract-new target (fixed in `197454e6`), one behavioural drift (fixed here), three stale docs
and two stale code comments, and a new data gate that itself shipped a false-positive draft. The
drift and the false positive are the two lessons; the rest is bookkeeping.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED | `TaomPartyWageModel.GetTroopRecruitmentCost` is a full replacement of the vanilla method and still carried the v1.4.8 shape of the buyer-perk discounts (each on the BUYER's own perks, `Frugal` gated on `IsPartyLeader`, `LimitMin(1f)` inside the null check). v1.5.2 resolves the seven secondary-role discounts through `PerkHelper.AddPerkBonusForParty` on the buyer's party; all seven have `SecondaryRole PartyLeader`, so they are the party leader's perks, none without a party, no leader gate, clamp outside the block. | GameModel full-replacement drift | Every gate TAOM has compares signatures. A full-replacement override is, by construction, a frozen copy of a vanilla body, and nothing diffed that body against the new engine. The CHANGELOG entry that introduced it (2026-H1 archive, "restore buyer-hero recruitment-cost perk discounts") records the v1.4.5 body it was verified against; no test or doc names it as a copy to be re-diffed on a bump. | The body diff is now a step of the engine bump (this pass); the lesson below asks every full-replacement override to name the vanilla body it copies so the next diff can rank it first. Fixed in this commit; `WageModifierServiceTests` pins the ungated Frugal and the buyer-less clamp. |
| 2 | LOW | The first draft of `XsltTemplateCoverageTests` evaluated each stylesheet template against each vanilla file of the same basename separately and reported 156 dead templates, 140 of them false: `Native`, `SandBox` and `StoryMode` all ship a `module_strings.xml`, and a template that hits one file misses the others. | Test models the engine wrongly | The Python prototype (`resource_checks.py`) had the same per-file loop and happened to print only the first 12 misses per file, so the false positives never showed. The engine merges every earlier module's file before it applies a stylesheet (`MBObjectManager.CreateMergedXmlFile`), so the unit of coverage is the union, not the file. | Test rewritten to count hits across the union; the false-positive count is recorded in its doc comment so the next author does not "fix" it back. |
| 3 | LOW | The same test re-parsed each XPath once per (template, file) pair instead of compiling it once per template. | Test efficiency | New code, one-shot test, no gate existed for it; the efficiency review agent read the loop. | `XPathExpression.Compile` once per template. |

Not findings, recorded because agents raised them and the evidence closed them:

- The data-flow agent read the seven perks' single trailing `EffectEnvironment` argument as their
  SECONDARY environment and called the "no naval scaling" comment wrong. `PerkObject.Initialize` has
  sixteen parameters; the fifteenth is `primaryEffectEnvironment`, the sixteenth
  (`secondaryEffectEnvironment`) defaults to `All`, and every one of the nine calls passes fifteen
  positional arguments. The API agent read the same signature and confirmed the comment.
- The same agent reported the three Gondor wives (`lord_1_9_5`, `lord_1_52_4`, `lord_1_71_1`) as
  one-directionally married because only the husbands carry `spouse=`. `Hero.Spouse`'s setter writes
  `_spouse.Spouse = this` (`Hero.cs`, v1.5.2), so the link is reciprocal at load. Vanilla's own
  `heroes.xml` declares both sides, but it does not have to.
- `TaomPartyWageModel.cs` is 199 lines. It was 195 before this change; the entry-point ceiling was
  already exceeded, and every override body is boundary conversion plus one delegate. The four
  added lines are comment and one local; noted for a later split, not fixed here (edit-scope
  discipline).

## Root-cause pattern

Both real findings are the same mistake at two scales: **a copy of engine behaviour that nothing
re-checks against the engine.** A full-replacement GameModel override copies a vanilla body; a
per-file XSLT check copies an assumption about how the engine reads files. Each was right when
written and had no mechanism to be found wrong later. The signature gates are strong precisely
because they re-derive from the installed DLLs every run; a copied body has no equivalent until
this pass.

## Why each review agent missed finding 1 at the compile commit

The compile commit's five deep-review agents were scoped to the files that commit changed, and
`TaomPartyWageModel.cs` was not one of them: its signature did not move, so the compiler never
named it. Agent 2 (API compatibility) verifies that a used member exists with the expected
signature; it does not diff bodies. Agent 5 (data flow) traces TAOM data across TAOM files. No agent
prompt asks "which TAOM overrides replace a vanilla body wholesale, and has that body changed". The
body diff answers that question for the whole bound surface, which is why it is now a bump step.

## Feedback memories to codify

Two lessons appended to the category files (`docs/reviews/lessons/gamemodels-services.md`,
`docs/reviews/lessons/xslt-moduledata.md`). No new harness rule: the `/engine-bump` skill gains the
body-diff step in the docs commit that follows, and that is the mechanism, not a rule file.

## Gates at this commit

Build 0 errors. Suite 9,173 passed / 2 skipped / 0 failed of 9,175. `XsltTemplateCoverageTests`
green with the 16 + 3 dead templates removed. Deep review: standards pass, API 18 verified / 0
incompatible, efficiency one LOW (fixed), completeness complete, data flow 0 gaps.

## Cross-references

- [rca-v1.5.2-compile-2026-09-14.md](rca-v1.5.2-compile-2026-09-14.md), the compile-level pass
- [docs/features/troop-progression.md](../features/troop-progression.md)
- [docs/reviews/lessons/gamemodels-services.md](lessons/gamemodels-services.md)
- [docs/reviews/lessons/xslt-moduledata.md](lessons/xslt-moduledata.md)
