# Deep review: plan 055, enlistment redirect in the grace window inside a settlement (2026-11-18)

```
DEEP REVIEW REPORT
===================
Feature: Keep the settlement-menu redirect armed in CommanderUnavailable while the player is
         inside a settlement with no PlayerEncounter (improve plan 055, #511)
Date: 2026-11-18
Range: c63dbb0ce..169b8bc72 (branch improve/055-enlistment-redirect-outside-attached), then the
       review follow-up commit on top of it

Scope:   C# (Main/Features/Enlistment, 4 files: one gate change in EnlistmentMenuService, three
         comment edits), tests (EnlistmentMenuServiceTests, 24 new results), docs (enlistment.md,
         return-to-army.md, harmony-patch-registry.md). No XML, no hooks, no validators.
Blast radius: the changed method TryRedirectMenu has two callers, both Patch66 hooks
         (GameMenuManagerSetNextMenuPatch.cs:29, MapStateEnterMenuModePatch.cs:27), with no gate of
         their own above the call (Lens 5, flow 2). The new private helper reads only the existing
         IMobilePartyAttachmentAdapter.GetPresenceFlags. No public signature changed.
Waves:   one wave: Agent 2 (Engine compatibility) and Agent 5 (Data flow). Agents 1, 3, 4 and 6
         SKIPPED BY TIER (tier 1, campaign menus). Codex not run. No convergence pass: Step 4
         applied doc text only, no code.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: PASS: 0 incompatible, 0 unverified in the changed hunks (11 claims verified);
               2 FOLLOW-UP notes outside scope, one of them now verified by the lead
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS: 9 flows traced, 0 gaps, 0 inconsistencies; 2 LOW pre-existing FOLLOW-UPs
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## DETAILS

### Agent 1: Standards

SKIPPED BY TIER.

### Agent 2: Engine compatibility

No incompatibility and no defect. The changed C# calls no new TaleWorlds API. The lead re-read the
load-bearing engine claims in the installed v1.5.7 cache (`taom-src` cache v1.5.7-202611101605):

- `DefaultEncounterGameMenuModel.GetGenericStateMenu` (`:254-333`): inside a fortification with no
  siege and no settlement map event, the result is `castle_outside` (`:305`) or `town_outside`
  (`:311`), and a village with no waiting encounter reaches `return null;` (`:333`).
- `game_menu_town_outside_on_init` and `game_menu_castle_outside_on_init` both read
  `PlayerEncounter.EncounterSettlement` into a local and set `args.MenuTitle = encounterSettlement.Name`
  on the next line. The lens rated the doc's one-line paraphrase a LOW wording imprecision; the lead
  reworded the doc sentence to describe the two statements (Step 4, applied).
- `GameMenuManager.SetNextMenu` (`GameMenuManager.cs:42-45`) is still the single statement
  `NextGameMenuId = name;`. The registry's "verified 1.4.7" citation now also names v1.5.7.

FOLLOW-UP 1 (pre-existing, both states): under a siege, `GetGenericStateMenu` can return
`menu_siege_strategies`, `encounter_interrupted_siege_preparations` or `continue_siege_after_attack`
(`:259-285`); none is in `RedirectMenuIds`, so they flow in `EnlistedAttached` and in grace alike.
Whether their inits survive with no `PlayerEncounter` stays UNVERIFIED (the lead did not open those
init handlers). Recorded as "Also open" in the feature doc's #511 section. No issue filed: this run
takes no GitHub action.

FOLLOW-UP 2 (the settlement's own battle in grace): the lens left two points UNVERIFIED. The lead
checked both. With no siege and a settlement map event, `GetGenericStateMenu` returns `encounter` or
`join_encounter` (`:287-294`). `game_menu_join_encounter_on_init` reads
`PlayerEncounter.EncounteredBattle` (`EncounterGameMenuBehavior.cs:645`), whose getter dereferences
`Current._encounteredParty` (`PlayerEncounter.cs:164-170`) with no null check, and
`game_menu_encounter_on_init` calls `PlayerEncounter.StartBattle()` when `Battle` is null and the main
party has no map event (`EncounterGameMenuBehavior.cs:1465-1473`), which is
`Current.StartBattleInternal()` (`PlayerEncounter.cs:890-893`). Both throw with no encounter, so masking
them in grace removes a crash and is not a gameplay change beyond the fix; it is the same handling an
`EnlistedAttached` player in that shape already had. The effect (the grace player is held in the
wait menu and not offered the defence from this shape) is now one paragraph in the feature doc, as
the lens recommended.

### Agent 3: Efficiency

SKIPPED BY TIER.

### Agent 4: Completeness

SKIPPED BY TIER.

### Agent 5: Data flow

9 flows traced, all CONNECTED. The lead re-read the flows the verdict rests on:

- `EnlistmentMenuService.cs:112-114`: the state read comes first, and
  `IsGraceInsideSettlementWithoutEncounter` (`:184-191`) returns false before any presence read for
  every state but `CommanderUnavailable`. The hot-path test
  `TryRedirectMenu_EveryStateButCommanderUnavailable_DoesNotReadPresenceForANonBattleMenu` pins it.
- The battle exception (`:124`) and the shore-leave exception (`:138`) still run after the gate and
  before the id lookup (`:144`), so both outrank the grace mask; two new tests pin each.
- The follow path opens no encounter: `ServiceAttachmentService.cs:203` calls `MoveIntoSettlement`,
  and the adapter's `MoveIntoSettlement` (`MobilePartyAttachmentAdapter.cs:308-322`) places the party
  with no `PlayerEncounter.Start`.

FOLLOW-UP 1 (LOW, pre-existing, outside the diff): `EnlistmentReconciler.cs:390-392` says "Since #510
every settlement placement opens an encounter deliberately", and `ServiceAttachmentService.cs:79-80`
says the same. CONFIRMED as stale: the follow path above opens none, and #511 rests on that. Plan 055 put
`EnlistmentReconciler.cs` and `ServiceAttachmentService.cs` out of scope, so it is not edited here. No
issue filed: this run takes no GitHub action.

FOLLOW-UP 2 (LOW): the unknown-id diagnostic at `EnlistmentMenuService.cs:156` says "pushed while
attached and NOT redirected". CONFIRMED: after this change it can also print in the grace window. It
is log text only, with no effect on a decision. Plan 055 names this line and leaves it on purpose
("a wording change is not needed for the fix"), and the line is outside the changed hunks, so Step 4
rule 2 lists it and does not apply it. The one-word fix ("while serving") belongs with the #511
follow-up.

### Agent 6: Design and elegance

SKIPPED BY TIER.

## ACTION ITEMS

1. None blocking. No defect was found in the changed code.
2. For the maintainer: the in-game check owed on #511 stays owed (no game run in this review).
3. For the maintainer: the stale "#510 every settlement placement opens an encounter" comments
   (Lens 5 FOLLOW-UP 1), the "while attached" log wording (Lens 5 FOLLOW-UP 2) and the siege-menu
   question (Lens 2 FOLLOW-UP 1) can go into one #511 follow-up.

## IMPROVEMENTS (Step 4)

Agents 3 and 6 were skipped by tier, so there were no APPLY-scoped or KEEP proposals. The lens notes
on the changed docs were applied as text; no code changed.

- APPLIED: `docs/features/enlistment.md`, #511 section: the `town_outside` and `castle_outside` init
  sentence now describes the two engine statements instead of a one-line paraphrase (Lens 2 wording
  LOW); a paragraph records that the mask also takes the settlement's own battle menus and why both
  inits would throw (Lens 2 FOLLOW-UP 2, verified by the lead); and an "Also open" sentence records
  the siege menus (Lens 2 FOLLOW-UP 1). `docs/reference/harmony-patch-registry.md`: the
  `SetNextMenu` citation names v1.5.7 too. Proof: doc text only; `python tools/lint_docs.py
  --fail-on-drift` exit 0, and the full suite before and after is in VERIFICATION.
- NOT APPLIED: `Main/Features/Enlistment/EnlistmentMenuService.cs:156`, "while attached" log wording:
  outside the changed hunks and named out of scope by plan 055.
- FOLLOW-UP: Lens 5 FOLLOW-UP 1 and 2, Lens 2 FOLLOW-UP 1, above. No issue filed for any of them,
  because this run takes no GitHub action.

## VERIFICATION

- Base suite recorded by plan 055 at 3164713da (c63dbb0ce adds plans only):
  `Failed: 3, Passed: 42911, Skipped: 9, Total: 42923`, the three localization tests
  `EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems` and
  `EveryKey_IsSeededInAllTwelveLanguageFiles` failing (the language files are the project lead's).
- Full suite at 169b8bc72, started before any edit:
  `Failed!  - Failed:     3, Passed: 42935, Skipped:     9, Total: 42947`, the same three
  localization tests failing. 42935 is the base's 42911 plus the 24 new results.
- Full suite after the follow-up edits (doc text only; rerun because some gate tests read docs):
  `Failed!  - Failed:     3, Passed: 42935, Skipped:     9, Total: 42947`, the same three failing.
- `python tools/lint_docs.py --fail-on-drift`: exit 0.
- Not run: the RefAsm no-game step and the hook suite (no test, hook or validator changed in this
  follow-up), any in-game check (standing call: no game run). No gate changed, so no differential
  sweep ran.

VERDICT: READY FOR COMMIT

## CODEX REVIEW

Codex not run (out of usage since 2026-11-15; none was dispatched for this item). The two lens
reports stand in for it. Phase 3d table over the lens findings:

| # | Source | Reported severity | Lead severity | Agree? | Reason |
|---|--------|------------------|---------------|--------|--------|
| 1 | Lens 2, `town_outside` paraphrase | LOW (wording) | LOW (wording) | Yes, not a defect | The init reads the property into a local first; the effect claimed is right. Doc reworded |
| 2 | Lens 2, registry "verified 1.4.7" | nit | nit | Yes | Still one statement in v1.5.7; citation extended |
| 3 | Lens 2 FOLLOW-UP 1, siege menus | FOLLOW-UP (UNVERIFIED) | FOLLOW-UP (UNVERIFIED) | Yes | Pre-existing, same in both states; recorded as open in the feature doc |
| 4 | Lens 2 FOLLOW-UP 2, settlement battle masked in grace | FOLLOW-UP (UNVERIFIED) | Not a defect, VERIFIED | Yes | Both inits dereference `PlayerEncounter.Current`; masking removes a crash. Doc paragraph added |
| 5 | Lens 5 FOLLOW-UP 1, stale #510 comments | LOW (pre-existing) | LOW (pre-existing) | Yes | Confirmed stale; files out of plan scope |
| 6 | Lens 5 FOLLOW-UP 2, "while attached" log text | LOW | LOW | Yes | Confirmed; log only, plan keeps it; not applied |

- Confirmed bugs in the changed code: none.
- False positives: none.
- Design questions: none that need the maintainer; row 4 is crash removal, the standing call's scope.
- Things the lenses found outside the change: rows 3, 5 and 6.

### Codex lessons (pending)

None: Codex did not run, so there is no false-positive pattern or missed category to record.
