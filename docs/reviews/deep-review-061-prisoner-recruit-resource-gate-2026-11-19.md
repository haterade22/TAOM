# Deep review: plan 061, clamp party-screen prisoner recruitment to the recruit cost (#563)

```
DEEP REVIEW REPORT
===================
Feature: Special resources: clamp party-screen prisoner recruitment to the recruit cost (#563), plan 061
Date: 2026-11-19

Scope:   C# (PartyScreenLogic_AddCommand_Patch GateRecruit and RecruitedThisScreen,
         SpecialResourceService.ClampRecruitCount and RecruitCostOf, the hook and its interface,
         SpecialResourceMessages), XML (troop_resource_costs.xml comments, one row in
         taom_module_strings.xml), 5 test files, 4 docs.
         Diff af0f5f330..c88502947 on improve/061-prisoner-recruit-resource-gate.
Blast radius: graphify graph MISSING for this worktree (status --brief), so not used. git grep -w:
         IOnPartyUpgradeResourceCheck has one implementer (PartyUpgradeResourceCheckHook) and three
         patch consumers; ISpecialResourceService has one implementer (SpecialResourceService). The
         two added interface members compile against every implementer (the suite built).
Waves:   one wave: Agent 2 (Engine compatibility), Agent 5 (Data flow), Agent 7 (XML and ModuleData).
         Tier 1: a Harmony prefix on PartyScreenLogic.AddCommand.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: PASS, 0 incompatible, 1 unverified (co-op, no session run); 14 verified
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS, 0 gaps, 1 inconsistency (co-op, LOW, documented; NEEDS MIKE)
DESIGN:        SKIPPED BY TIER
XML:           PASS, 6 gates (0 failed, 1 not run by the lens: the localization tests, run by the lead), 0 findings
TOOLING:       NOT IN SCOPE
```

## Details

### Agent 1, Standards

SKIPPED BY TIER (deep-review tier 1 rule).

### Agent 2, Engine compatibility

14 verified against the installed v1.5.7 DLLs, 0 incompatible, 1 unverified (co-op). The lead re-read
the load-bearing engine lines in the taom-src cache `v1.5.7-202611101605`:

- `PartyScreenLogic.AddCommand` calls only `ProcessCommand` (PartyScreenLogic.cs:592-595), so a prefix
  returning false skips only `RecruitPrisoner`.
- `RecruitPrisoner` moves `Min(RightRecruitableData[character], TotalNumber)` and writes the history
  through `AddRecruitToHistory` (PartyScreenLogic.cs:942-969, :1477-1489). It raises `Update` only when
  it moved a troop, so a refused command raises no VM callback.
- `DoneLogic` fires `FireCampaignRelatedEvents` (:1264), which raises `OnMainPartyPrisonerRecruited` from
  the full `RecruitedPrisonersHistory` (:1295-1310), then empties the history (:1271). The event fires
  for every party screen, whoever owns the right side, so the clamp never refuses a recruit that would
  go uncharged in single-player.
- `ResetLogic` copies the history from `_initialData` (:1530-1536); `ResetToLastSavedPartyScreenData`
  copies it from `_savedData` (:1546-1557). Both raise `AfterReset`.
- The lead decompiled `PartyTroopManagerPopUp.PartyTroopManagerVM` with ilspycmd from the installed
  `TaleWorlds.CampaignSystem.ViewModelCollection.dll`: `OpenPopUp` calls `SavePartyScreenData()`
  (line 533) and `ConfirmCancel` calls `ResetToLastSavedPartyScreenData(fromCancel: false)` (line 554).
  This confirms Agent 2. Agent 5's "no caller of `SavePartyScreenData`" was a negative scoped to the
  taom-src cache, which holds no `PartyTroopManagerVM` file; it changes no verdict, because a restore
  from saved data puts the history and the rosters back together.
- `PartyVM.ExecuteRecruit` (PartyVM.cs:2925-2946) sends 1, `Min(NumOfRecruitablePrisoners, 5)` or
  `NumOfRecruitablePrisoners`, and `NumOfRecruitablePrisoners` is
  `GetTroopRecruitableAmount(Character)` (PartyCharacterVM.cs:1244), which returns
  `RightRecruitableData[troop]` (PartyScreenLogic.cs:1571-1578).

Co-op (UNVERIFIED, no session run): the prefix runs in the process that drives the party screen. On a
guest it reads the guest's `Hero.MainHero` and its local, possibly stale, balance. What the code does:
`GateRecruit` has no session check, so in a session it cuts or refuses a recruit against that peer's
local balance, while Coop's prisoner path charges nothing (special-resources.md "Co-op recruit
charge"). See NEEDS MIKE 1.

FOLLOW-UP from this lens (pre-existing code):

1. MEDIUM. A Recruit or Upgrade pop-up Cancel calls `ResetToLastSavedPartyScreenData`, which raises
   `AfterReset`; `SpecialResourcesBehavior.OnPartyScreenReset` (SpecialResourcesBehavior.cs:265-269)
   then calls `CancelSession()` and drops all pending upgrade spend, including upgrades queued before
   the pop-up opened, which the restore keeps. The lead confirmed both halves (the decompile lines
   above and SpecialResourceService.cs:439-445). Effect: the upgrade commit undercharges, and the new
   recruit clamp reads a balance too high by that amount. The recruit charge at Done then floors at
   zero, which is the pre-#563 behaviour, so this change makes nothing worse. No issue filed: an
   /improve lead files none; the orchestrator decides at wrap-up.
2. LOW, toast repetition in "Recruit All": each refused troop type adds its own yellow "Requires N"
   line. Lead verdict: FALSE POSITIVE as a defect. Each line is the price of a different type, the
   count is bounded by the prisoner types on screen, and the upgrade path shows nothing at all when it
   refuses. In-game look owed with the #563 check.

### Agent 3, Efficiency

SKIPPED BY TIER.

### Agent 4, Completeness

SKIPPED BY TIER.

### Agent 5, Data flow

11 flows traced, 0 gaps, 1 inconsistency.

- Flows 1 to 4 and 6 to 11 connected. The lead re-read the clamp (SpecialResourceService.cs:280-306)
  and the charge (:237-259): both resolve the resource from the hero's kingdom and culture, both price
  with the nominal `recruit_cost`, and both read the same config provider (one singleton
  `PartyUpgradeResourceCheckHook(ISpecialResourceService, ISpecialResourceConfigProvider)`,
  SpecialResourcesIoC.cs:21). Queued upgrades come off through `GetAvailableAfterPending` (:384-389);
  earlier recruits in the screen come off through `RecruitCostOf(recruitedThisSession)`.
- Flow 5, co-op, LOW, documented (special-resources.md, the prisoner recruit gate paragraph, the
  "Not effective yet" paragraph and the 2026-11-19 changelog line). The lens recommends the maintainer
  accept it or gate `GateRecruit` on no active session until #967. NEEDS MIKE 1.
- Probe note, LOW: the yellow clamp line is shown before the engine applies its own
  `RightRecruitableData` cap, so it could name more recruits than move. Lead verdict: FALSE POSITIVE.
  The VM's request never exceeds `RightRecruitableData` (Agent 2 section above), and the clamp only
  lowers it, so the count in the line is the count the engine moves.
- `RecruitedThisScreen` (the conversion from the engine's tuples) has no unit test because it binds to
  `PartyScreenLogic`; the service tests cover the arithmetic it feeds. In-game check owed.

FOLLOW-UP from this lens: `AttachToPartyScreen` returns early while `_activePartyScreenLogic` is set
(SpecialResourcesBehavior.cs:228), so a screen that closed without `PartyScreenClosedEvent` would leave
the next one unattached. Hypothetical, LOW, pre-existing; no issue filed (nothing in scope shows it
happens).

### Agent 6, Design and elegance

SKIPPED BY TIER.

### Agent 7, XML and ModuleData integrity

0 findings. `python -B tools/data_gates.py` on the two files: 5 PASS, 0 FAIL, 1 NOT RUN (the
localization tests, which the lead ran in the full suite below). Comment-only changes in
`troop_resource_costs.xml`; one new row `taom_res_recruit_clamped` in `taom_module_strings.xml`
matching `SpecialResourceMessages.cs:47`; CRLF and no BOM kept. No language file has the new key yet,
which the orchestrator note expects until the project lead's pass.

OWED in game: a War Spoils player with a balance below 50 and a captured cave troll; the recruit is
refused with the "Requires" line. With 100 and three trolls, only 2 are recruited, with the
"Only enough resources for 2 recruit(s)." line.

### Tooling correctness

NOT IN SCOPE (no hook, validator or tool in the diff). No gate changed, so no differential sweep.

## Action items

None in the changed code. Decision owed: NEEDS MIKE 1.

## NEEDS MIKE

1. Co-op: in a session the clamp cuts or refuses a party-screen prisoner recruit against the acting
   peer's own (on a guest, possibly stale) balance, although Coop's prisoner path charges nothing.
   Option A of #563 approved the single-player charge; the co-op restriction is documented but not
   explicitly accepted. Choices: accept it until #967, or skip `GateRecruit` while a co-op session is
   active. Lead recommendation: skip it in a session, since the clamp guards a charge that never
   happens there. Not applied: it changes behaviour, and the lead applies no behaviour change without
   the maintainer's word.

## Improvements (Step 4)

- APPLIED: none. Agents 3 and 6 were skipped by tier, so no APPLY-scoped fix or KEEP proposal exists.
- NOT APPLIED: the co-op session skip (Agent 5 flow 5), behaviour-changing; NEEDS MIKE 1.
- FOLLOW-UP: the pop-up Cancel dropping all pending upgrade spend (Agent 2, MEDIUM, pre-existing) and
  the `AttachToPartyScreen` early return (Agent 5, LOW, hypothetical). No issue filed: an /improve
  lead files none, and the orchestrator decides at wrap-up.

## Verification

- Full suite at the branch head `c88502947`, no edit made by the lead:
  `Failed!  - Failed:     3, Passed: 43046, Skipped:     9, Total: 43058`.
- The three failures are `EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems` and `EveryKey_IsSeededInAllTwelveLanguageFiles`, the known
  language-file failures. Their cited causes exist at the base: `git grep` finds no
  `TAOM_LD_Hud_UpgradeDeal` in the base's `Languages/BR/` and finds the English word in
  `taom_hostile_peace_mcm_alliances_hint` in the base's RU file; the diff changes no language file. The
  new key adds one missing row per language to the first test, which already fails.
- The lead did not run a separate base suite: it made no code edit, so the head run is the only run
  the verdict needs.
- Not covered: no game run (tier 1 rule for this item); no co-op session; the RefAsm no-game replay was
  not run (no new engine-bound test); no tools or hooks changed, so the Python and hook suites were not
  run.

## Codex review

Codex not run for this item (no paid dispatch was authorised for it).

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| - | Codex not run | - | - | - |

Codex lessons (pending): none. With no Codex run there is no false-positive pattern or missed category
to record.

VERDICT: READY FOR COMMIT
