# Deep review: plan 049, the refuge save-load repair unit (2026-11-13)

```
DEEP REVIEW REPORT
===================
Feature: Extract RefugeService's save-load repair into one unit with lazy clock seams (plan 049, Refs #1132)
Date: 2026-11-13

Scope:   C# only. Branch improve/049-refuge-load-repair-unit, diff 664f19c5b..9b783ca9a:
         Main/Features/Refuge/Domain/RefugeBookRepair.cs (new), Main/Features/Refuge/RefugeService.cs,
         TAOM.Tests/Features/Refuge/RefugeBookRepairTests.cs (new). No XML, no scripts, no live files.
Blast radius: 1 type changed in its public surface (RefugeService; LoadFrom keeps its signature).
         The only production caller of LoadFrom is Main/Features/Refuge/Hooks/RefugeCampaignBehavior.cs:88,
         unchanged. RefugeServiceTests.cs is unchanged. RefugeData.cs and RefugeSaveDefiner.cs are unchanged,
         so the save format is unchanged.
Waves:   Tier 1 (the save-load path of a campaign behaviour's SyncData). One wave: Agent 5 Data flow,
         Agent 2 Engine compatibility. No CRITICAL or HIGH, so no second wave.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: PASS - 0 incompatible, 0 unverified (10 verified); 1 LOW comment finding, confirmed and fixed
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS - 0 gaps, 0 inconsistencies (13 flows traced)
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Details

### Agent 2, Engine compatibility

- Every engine member the change touches was checked against the installed v1.5.7 DLLs (taom-src cache
  `v1.5.7-202611101605`) and the RefAsm stubs: `CampaignTime`, `Now`, `Never`, `operator -`, `ToDays`,
  `Equals(CampaignTime)`, `Campaign.Current`. All compatible.
- The lens read the IL of the built `TAOM.dll`: `LoadFrom` binds both seams with `ldvirtftn`, so the
  subclass overrides dispatch, and it calls neither seam itself. In `RepairRow` the `Building` test comes
  before the `daysSince` invoke, and the future-start branch comes before the `now` invoke. The clock
  reads stay lazy, which is the trap the plan names (ARCH-03).
- The load call chain (`Campaign.cs:1448` to `RefugeCampaignBehavior.cs:82-88`) is unchanged, and
  `CampaignTime.Initialize` runs before it (`Campaign.cs:1413`).
- No reflection or by-name consumer of the removed `RepairLoadedRow` or the old private constant.
- **Finding L2-1 (LOW), CONFIRMED:** the comment at `RefugeBookRepairTests.cs:203-205` said
  `CampaignTime.Never` "is the one nonzero CampaignTime a test can build without a campaign". That is
  false as written: `operator -` (CampaignTime.cs:347-350) on `Zero` and `Never` builds another nonzero
  value with no campaign. What holds is narrower: `Never` (CampaignTime.cs:72) is the only public
  factory that is nonzero without a campaign, because `Milliseconds` to `Years` (CampaignTime.cs:272-339)
  multiply by tick statics (CampaignTime.cs:39-47) that only `Initialize` (CampaignTime.cs:178-198) sets,
  and the `*FromNow` factories and `DeltaTime` read `Campaign.Current`. The text came from the plan
  (`plans/049-refuge-load-repair-unit.md:603`). Fixed: the comment now states the narrower claim.
- **FOLLOW-UP (pre-existing text, moved verbatim):** the comments at `RefugeBookRepair.cs:50-53` and
  `:72-73` still say a NaN target never reaches `BuildProgress >= 1`. `RefugeData.BuildProgress` already
  returns 1 for NaN and negative infinity (`RefugeData.cs:61-62`); only positive infinity never finishes.
  Not reworded here: the plan's move check (`check_refuge_repair_texts.py`) compares the moved bodies
  line for line against the base, and a reword in the same commit would defeat it. no issue: an
  /improve agent files none; the orchestrator decides at wrap-up.
- **FOLLOW-UP (pre-existing, unchanged file):** `RefugeServiceTests.cs:1268-1270` carries the same loose
  "only ... nonzero CampaignTime a test can construct" wording. no issue: same reason.

### Agent 5, Data flow

- 13 flows traced, all CONNECTED: the book instance from SyncData through the repair and back to
  `SaveInto` by reference; the lazy seams; the one `MinBuildTargetHours` constant now shared by the load
  floor and `SaneBuildHours`; the sign agreement of the future-start check and `ElapsedHoursUntilNow`;
  enum coverage of `RefugeTier`; row identity; the militia floors; NaN polarity; the `LoadFrom` resets,
  which still run after the repair in the base's order.
- **FOLLOW-UP:** `RefugeService.cs:617-618` and `RefugeCampaignBehavior.cs:63-64` call the load-time
  disband cancel "the LoadFrom belt". Checked: the base `LoadFrom` (`git show 664f19c5b`, lines 643-666)
  has no disband cancel; the load-time cancel is in `OnGameLoaded` (`RefugeService.cs:671`, the call at
  `:711`). Pre-existing and outside the changed hunks. no issue: same reason.
- **FOLLOW-UP:** `docs/features/refuge.md:112-117` ("Load repairs hostile rows") leaves out the
  future-`BuildStartTime` reset, and the Key files table (`refuge.md:162-175`) has no row for
  `Domain/RefugeBookRepair.cs`. The plan (lines 1042-1044) gives the row to the orchestrator. no issue:
  the orchestrator's item.

## Action items

1. L2-1: correct the test comment. Done in this review's commit.

## Verification (run by me in the worktree)

- Base, at 9b783ca9a before any edit: `Passed!  - Failed:     0, Passed: 39266, Skipped:     9, Total: 39275`.
- After the fix: `Passed!  - Failed:     0, Passed: 39266, Skipped:     9, Total: 39275`, matching the base.
- `python tools/lint_docs.py --fail-on-drift`: exit 0, 0 em or en dashes in new prose.
- The fix touches only a comment in a test that already carries `[TestCategory("RequiresGame")]`, so the
  RefAsm step is unaffected; the executor's RefAsm log (`Passed: 15, Total: 15`) was read, not re-run.
- No hook, gate or validator changed, so no differential sweep and no hook suite run.
- No in-game check: another session holds the game install. The change is a behaviour-preserving move
  covered by 154 `RefugeServiceTests` and 16 `RefugeBookRepairTests`.

```
─────────────────────────
IMPROVEMENTS (Step 4)
─────────────────────────
APPLIED:     none. Agent 3 and Agent 6 did not run (tier 1 first wave only), so there were no proposals.
             The one change in this review is the L2-1 defect fix: RefugeBookRepairTests.cs:203-205,
             comment only, guarded by Repair_BuildingRowStartedInTheFuture_ResetsToNow (unchanged).
NOT APPLIED: none.
FOLLOW-UP:   RefugeBookRepair.cs:50-53 and :72-73, stale NaN wording (no issue: orchestrator, wrap-up);
             RefugeServiceTests.cs:1268-1270, loose "only nonzero" wording (no issue: same);
             RefugeService.cs:617-618 and RefugeCampaignBehavior.cs:63-64, "the LoadFrom belt" should be
             "the OnGameLoaded belt" (no issue: same);
             refuge.md:112-117 and the Key files row for Domain/RefugeBookRepair.cs (no issue: the
             orchestrator's item per the plan).

VERDICT: READY FOR COMMIT
```

## Codex review

Codex not run. Codex is out of usage, and a paid call needs the maintainer's word, so the Lens 5 and
Lens 2 rounds stand in for it. Phase 3d assessment:

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | not run | n/a | n/a | No Codex review exists for this item |

- **Confirmed bugs:** none from Codex. From the lenses: L2-1 (LOW, comment), fixed.
- **False positives:** none.
- **Design questions:** none.
- **Things Codex missed:** not applicable.

### Codex lessons (pending)

None. No Codex pass ran, so there is no new false-positive pattern and no missed category to record.

RCA: [rca-refuge-load-repair-unit-2026-11-13.md](rca-refuge-load-repair-unit-2026-11-13.md).
