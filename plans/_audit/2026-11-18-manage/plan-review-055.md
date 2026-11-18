# Cold review: plan 055, round 1

- Plan: `plans/055-enlistment-redirect-outside-attached.md`
- Code read at: `3164713da` (run worktree HEAD, `git rev-parse --short HEAD` printed `3164713da`)
- Earlier reviews of this plan: none in `plans/_audit/2026-11-18-manage/` (only `PROGRESS.md` there).
- Engine reads: `pwsh tools/taom-src.ps1 path DefaultEncounterGameMenuModel` resolved the v1.5.7 cache
  (`detected version v1.5.7`); the other engine files were read from that same cache folder.
- Not run (read-only role): dotnet build or test, the Python suite, `lint_docs.py`. The baseline totals
  in Status (plan lines 22 to 28) are UNVERIFIED by this review.

## Verdict

One blocking item. The mechanics (excerpts, line numbers, test arithmetic, grep checks) hold, but the
chosen gate also catches a grace-window player who enters a settlement on his own, which the plan never
considers and which turns the fix into a new hold.

## Blocking

### B1. The gate `CommanderUnavailable && IsInSettlement` also catches a self-entered settlement visit (plan lines 41 to 43, 282 to 287, 602 to 616)

The plan claims the new handling is "exactly the handling an `EnlistedAttached` soldier in the same
place already gets" and that the only shape inside a settlement in grace is the follow path's
encounter-less one (lines 132 to 164). The repo says a grace player can be loose on the map:

- `Main/Features/Enlistment/ServiceMaintenanceService.cs:201` to `227`: a shore-leave pass is revoked
  on any state change (`TownLeavePolicy.ShouldRevokeLeave`, `Main/Features/Enlistment/TownLeavePolicy.cs:51`,
  "a commander taken prisoner must end the pass"), and the revoke calls Finish with the player walked
  out: "Out on the map with time stopped is merely paused." The service wait menu is not re-asserted,
  because `EnsureServiceMenu` is gated on `EnlistedAttached` (`ServiceMaintenanceService.cs:497`).
- `Main/Features/Enlistment/EnlistmentReconciler.cs` (`ReconcileGrace` comment, "visible and alone on
  the map"), `Main/Features/Enlistment/Duties/FieldDutyRuntime.cs:203` to `206` ("set you loose"), and
  the existing test comment the plan rewrites (`EnlistmentMenuServiceTests.cs:284`, "grace: the player
  roams free").

A loose grace player who clicks a friendly town or castle runs vanilla `PlayerEncounter.Init`
(v1.5.7 cache, `TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.cs`): line 669 computes
`encounterMenu` (`town_outside` / `castle_outside`, `DefaultEncounterGameMenuModel.cs:112`, `118`),
line 708 calls `EnterSettlement()` (which runs `EnterSettlementAction.ApplyForParty`, line 1595), and
only then line 710 calls `GameMenu.ActivateGameMenu(encounterMenu)`. So at the `SetNextMenu` prefix the
party already has `CurrentSettlement` set, `IsInSettlement` is true, and the planned gate rewrites
`town_outside` to the service wait menu. The same holds for the later `town` / `castle` / `village`
switches.

Result after this plan: the player stands inside the town with a live settlement `PlayerEncounter` and
`LocationEncounter`, held in the service wait menu, whose only exits while serving are "Ask to be
released" (`EnlistmentWaitMenuOptions.cs:102` to `108`; the one isLeave option is hidden while serving,
line 121). He loses the town for up to 7 days, and the settlement encounter is the shape the pump's
comment says nothing else takes down ("Leaked, it blocks every main-party encounter for the rest of the
term", `ServiceMaintenanceService.cs:206` to `211`). Before this plan that visit flowed normally. A weak
executor following the plan ships this; no test, STOP condition or maintenance probe mentions it.

Suggested repair (the reviser decides): mask only the crash shape, which is inside a settlement with
NO encounter. `PlayerPresenceFlags` already carries `HasPlayerEncounter`
(`Main/Adapters/PlayerPresenceFlags.cs:21`, set from `PlayerEncounter.Current != null` in
`MobilePartyAttachmentAdapter.cs:210` to `232`). For example:

```csharp
    private bool IsGraceInsideSettlement(EnlistmentState state)
    {
        if (state != EnlistmentState.CommanderUnavailable)
            return false;
        var presence = _attachment.GetPresenceFlags();
        return presence.IsInSettlement && !presence.HasPlayerEncounter;
    }
```

That needs, in the plan: a new pin test (CommanderUnavailable, inside, `hasPlayerEncounter: true`:
`town_outside`, `castle_outside`, `town`, `castle`, `village` all flow), its own mutation proof in
Step 3 (the Step 3b gate without the encounter term must fail it), updated result counts (Step 2 line
557, Step 3b line 622, Step 5 lines 786 to 788, Done criteria lines 857 to 858), the helper's comment,
and every prescribed sentence that says "inside a settlement" (4a line 645, 4b line 662, 4c line 676,
4d line 690, 4e line 704, 4f lines 729 to 731, 4g line 761, commit body lines 813 to 815). If the reviser
keeps the wider gate instead, the plan must show with evidence that a grace player cannot enter a
settlement on his own, and the evidence above says he can.

## Non-blocking

1. Stale claim outside Scope (plan lines 343 to 351 and the grep at 768): `docs/features/return-to-army.md:184`
   to `185` says "the redirect is gated on `EnlistedAttached` before the list is read". After the fix
   `army_wait_at_settlement` is redirected in grace inside a settlement too. Either add this file and a
   one-sentence amendment to Scope, Step 4 and the drift check, or name it as deliberately left. The
   plan's grep pattern does not match its wording.
2. Step 4's second check (lines 770 to 771) is a comparison the executor must judge. State the numbers:
   `git grep -n -c "CommanderUnavailable" 3164713da -- docs/features/enlistment.md` prints
   `...enlistment.md:15` at the base (checked); the planned text removes 2 and adds 4, so the expected
   count is 17 (recount after B1's wording changes).
3. Commit draft trailers (lines 818 to 823): the continuation lines of `Rejected:` and `Not-tested:`
   start at column 0, so git does not parse them as one trailer each. Indent continuation lines, or put
   each trailer on one line.
4. Step 5 item 6 (line 784): `grep '^[-+]'` also prints the `--- a/` and `+++ b/` header lines; the
   expected result (lines 790 to 793) should say they appear, or use `grep -E '^[-+][^-+]'`.
5. Status baseline (lines 22 to 28) and Step 1 Verify (line 398): not re-run here (read-only role). `B`
   = 54 (line 395) was checked by counting results in the test file at 3164713da: 16 plain
   `[TestMethod]`s plus 38 data rows and plain tests in the `TryRedirect*` group.
6. Maintenance notes (lines 919 to 926) and the in-game check (lines 899 to 908) should add the case
   from B1 (grace player walks into a town himself; expect the town menu, not the wait menu) once B1 is
   settled.

## Checklist results

- Executable with only the plan and the repo: yes for mechanics (exact files, symbols, Edit-tool
  replacements, exact commands). The gap is B1, a design case the plan does not know about.
- Every step ends in a command with an exact expected result: yes, except Step 4's comparison
  (non-blocking 2).
- Current-state excerpts against 3164713da: all checked and matching. `EnlistmentMenuService.cs`
  fields 13 to 18, gate 112 to 113, `IsBattleMenu` 160, `IsSettlementMenu` 170 to 171, log line 155;
  test file field line 19, constructor line 30, comment 283 to 284, test 276 to 289; interface lines 3
  to 11; config line 39 and list 43 to 68; `DischargeService.cs` 150 to 154 and 251 to 252;
  `enlistment.md` 97, 120 to 121, 2104 and 2131; registry line 620 (the replaced substring occurs once,
  file is UTF-8 with BOM); `PlayerPresenceFlags` constructor and `IsInSettlement`; adapter line 40,
  210 to 232, 90, 322; `ServiceAttachmentService` 86 to 88 and 190; reconciler 416 and 846 to 861;
  pump 484 and 497; hooks line 29 and 27; `EnlistmentIoC.cs:54`. Engine: `Campaign.cs` 1012 to 1019,
  `MapState.cs` 62, `PlayerEncounter.cs` 346, `GameMenu.cs` 345 to 384, `GameMenuManager.cs` 42 to 45,
  `DefaultEncounterGameMenuModel.cs` 299 to 311, town_outside and castle_outside init 2279 and 2372,
  `PlayerTownVisitCampaignBehavior.cs` 198: all match. All seven in-scope files are CRLF (`file`).
- RED and mutation arithmetic re-derived by hand against the code: Step 2 gives 6 failures (only the
  inside-settlement rows), Step 3a gives 6 (five outside rows plus the `CommanderUnavailable` row of
  `TryRedirect_NonAttachedServiceStates_NeverRedirect`, since `encounter` with default presence is not
  in a map event), Step 3b gives 0. The test filter matches one class only.
- TDD order, issue line (#511), binding ADRs (002, 003, 004, 005, 007, 008) named, no protected file
  edits and no Step 0 needed, single-owner files listed as out of scope, STOP conditions specific,
  done criteria machine-checkable, planned-at commit and drift paths consistent with Scope (the drift
  list also covers the out-of-scope files the excerpts quote), non-deploying commands carry
  `-p:ModuleId=`, no worktree path or branch name, no CHANGELOG step: all hold.
- Dashes: the only em dashes in the plan sit inside quoted existing code (plan lines 96, 103, 634),
  which the executor does not add. The prescribed new text has none. No secret values.
