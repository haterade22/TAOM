# Deep review: plan 061, the prisoner recruit gate stands aside in a co-op session (#563 decision A)

```
DEEP REVIEW REPORT
===================
Feature: Special resources: the prisoner recruit gate stands aside in a co-op session (#563 decision A), plan 061
Date: 2026-11-19

Scope:   C# (PartyUpgradeResourceCheckHook.ClampRecruitCount and its constructor; one comment in
         SpecialResourcesBehavior), 4 test files (one new), 1 doc (special-resources.md).
         Commit 96ea28e4a only (85c75c374..96ea28e4a on improve/061-prisoner-recruit-resource-gate);
         the rest of the branch was reviewed in deep-review-061-prisoner-recruit-resource-gate-2026-11-19.md.
Blast radius: graphify not used (the earlier pass on this branch found no graph for this worktree).
         git grep -w: IOnPartyUpgradeResourceCheck has one implementer (PartyUpgradeResourceCheckHook),
         one container registration (SpecialResourcesIoC.cs:21), one production resolve (SubModule.cs:1932)
         and three patch consumers; ClampRecruitCount has one caller (GateRecruit). Three direct
         constructor calls in TAOM.Tests, all updated in the commit.
Waves:   one wave: Agent 2 (Engine compatibility), Agent 5 (Data flow). Tier 1 (a Harmony prefix's
         hook on PartyScreenLogic.AddCommand).

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: PASS, 0 incompatible, 1 unverified (the Coop-side claim the commit relies on; it predates the commit); 6 verified
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS, 0 gaps, 1 inconsistency (LOW, CONFIRMED and fixed: the patch registry entry)
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE (no XML or XSLT in the commit)
TOOLING:       NOT IN SCOPE
```

## Details

### Agent 1, Standards

SKIPPED BY TIER (deep-review tier 1 rule).

### Agent 2, Engine compatibility

6 verified, 0 incompatible, 1 unverified. The commit adds no TaleWorlds API, no Harmony target and no
reflection; its only new dependency is TAOM's `ICoopSessionProvider`. The lead re-read the load-bearing
lines in the worktree:

- The session is read live. `PartyUpgradeResourceCheckHook.cs:38-41` reads `_coop.IsSessionActive` on
  every call and stores only the provider, as `ICoopSessionProvider.cs:13-16` requires ("Read these LIVE
  at the point of use; never cache the result in a field"). The hook is a container singleton
  (`SpecialResourcesIoC.cs:21`), so this matters; `ClampRecruitCount_ReadsTheSessionOnEveryCall` pins it
  with `Returns(true, false)`.
- `GateRecruit` (`PartyScreenLogic_AddCommand_Patch.cs:82-83`) returns true when
  `clamped >= command.TotalNumber`, before both toast branches, so a session recruit passes with no
  message and no `SetTotalNumber`.
- The upgrade clamp (`ClampUpgradeCount`, `CanAffordUpgrade`) is outside the hunk and still runs in a
  session, as scoped.
- Predicate: `special-resources.md` "Co-op recruit charge" says a live session without a scope charges
  nobody ("the authority's own `MainHero` pays nothing during a session") and that Coop's prisoner path
  never raises `OnUnitRecruited`, "host included". So the charge is zero on host and guest, and
  `IsSessionActive` ("client or host", `ICoopSessionProvider.cs:20`) is the predicate that matches it.
  `IsCoopClient` would keep the gate on the host, which is charged nothing. `ShouldDeferToHost` was not
  re-read by the lead; Agent 2's and Agent 5's readings of it differ in detail but agree it is the wrong
  predicate, which changes no verdict.
- UNVERIFIED: "Coop's prisoner path charges nothing" is a claim about BannerlordCoop, not TaleWorlds, and
  was not decompiled in this review. It predates the commit and the doc already says "Not checked in a
  session".

### Agent 3, Efficiency

SKIPPED BY TIER.

### Agent 4, Completeness

SKIPPED BY TIER.

### Agent 5, Data flow

8 flows traced, 0 gaps, 1 inconsistency. The production container registers `ICoopSessionProvider`
(`CoopInteropIoC.cs:16`), DryIoc builds the hook from its constructor, and the hook's only consumer is
`GateRecruit`. `SpecialResourcesWiringTests` already registered a substitute provider; the lead checked
`SpecialResourceConfigProviderTextTests.cs:108`, which registers the feature without one but resolves only
`ISpecialResourceConfigProvider`, so the hook is never built there.

Finding D1 (LOW, CONFIRMED): the Patch26 entry in `docs/reference/harmony-patch-registry.md:225` said
the `AddCommand` prefix clamps every `RecruitTroop` and did not mention the co-op stand-aside. The
registry is the crash and co-op triage entry point (orientation.md "Registries"). The lead found the same
gap in the patch's own `GateRecruit` comment (`PartyScreenLogic_AddCommand_Patch.cs:71-73`), which still
said every recruit is charged and clamped. Both are fixed.

The lead also grepped the other `#563` and "prisoner recruit" mentions under `Main/` and `docs/`
(outside `docs/reviews/`): `ISpecialResourceService.cs:32` and `:46`, `SpecialResourceService.cs:249`,
`troop_resource_costs.xml:15`, `:35` and `:81`, and `dire-wolf.md:108` describe the single-player charge
and clamp, which are unchanged, and none claims co-op behaviour. They are left as they are.

Agent 5's wording note on the `CoopVeto` string (the comma placement) is accurate text and not a defect.

### Agent 6, Design and elegance

SKIPPED BY TIER.

### Agent 7, XML and ModuleData integrity

NOT IN SCOPE.

## Findings

| # | Source | Sev | Verdict | Finding | Resolution |
|---|---|---|---|---|---|
| D1 | Agent 5 | LOW | CONFIRMED | `harmony-patch-registry.md:225` and the `GateRecruit` comment describe the prisoner clamp without its co-op stand-aside | Registry entry and comment updated; the comment was rewritten in place so no line label moves |
| U1 | Agent 2 | n/a | UNVERIFIED, not a defect | Coop's prisoner path charges nothing (BannerlordCoop claim) | Predates the commit; the doc says "Not checked in a session" |

No false positives. No NEEDS MIKE item: the maintainer took the decision (#563 A, 2026-11-19).

## Action items

1. Done: D1, the registry entry and the patch comment.
2. Owed in game (not new): a co-op session prisoner recruit at zero balance, on host and guest, shows no
   "Requires" line and recruits the full count.

## Improvements (Step 4)

Lenses 3 and 6 were skipped by tier, so no APPLY-scoped fix or KEEP proposal exists.

- APPLIED: `TAOM.Tests/Features/SpecialResources/SpecialResourcesWiringTests.cs`, new test
  `RegisterSpecialResources_PartyUpgradeHook_ResolvesWithItsSessionProvider` (Agent 5's optional
  container pin for the hook's new dependency). Test only, behaviour preserving. Proved it can fail: with
  the substitute `ICoopSessionProvider` registration removed from `BuildContainer`, it failed with
  `Unable to resolve TAOM.Features.CoopInterop.ICoopSessionProvider as parameter "coop"`; with it back,
  it passes in the full suite.
- NOT APPLIED: none.
- FOLLOW-UP: none new. The first review's follow-up stands: a Recruit or Upgrade pop-up Cancel drops all
  pending upgrade spend (MEDIUM, pre-existing; no issue filed in this run, the orchestrator decides).

## Verification

- Base (85c75c374 plus 96ea28e4a, before any edit): `Failed!  - Failed:     3, Passed: 43049, Skipped:     9, Total: 43061`.
- After the fixes: `Failed!  - Failed:     3, Passed: 43050, Skipped:     9, Total: 43062`. The three
  failures are the known language-file tests (`EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems`, `EveryKey_IsSeededInAllTwelveLanguageFiles`), the same as the
  base; the extra pass is the new wiring test.
- A first attempt added a fourth comment line above `GateRecruit`; `ReflectionSiteBindingTests.EveryLineLabel_PointsAtALineThatNamesItsMember`
  failed on the shifted `PartyScreenLogic_AddCommand_Patch.cs:117` label, so the comment was folded
  into the existing three lines.
- No gate (hook or validator) changed, so no differential sweep was run. No game run.

## Codex review

Codex not run: no paid dispatch was authorised for this item.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| - | - | - | - | Codex not run |

Codex lessons (pending): none (Codex did not run, so there is no false-positive pattern or missed
category to record).

## RCA

`docs/reviews/rca-prisoner-recruit-resource-gate-decision-2026-11-19.md`: D1 repeats a recorded cause
(a decision changed a patch's behaviour and the registry kept the old description, plan 019).

VERDICT: READY FOR COMMIT
