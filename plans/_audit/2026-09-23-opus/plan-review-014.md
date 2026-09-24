# Cold review: plan 014 (Enlistment session scope)

Reviewer: cold read, no prior context. Plan: `plans/014-enlistment-session-scope.md` (1245 lines).
Template: `.claude/skills/improve/references/plan-template.md`, "Quality bar".
Evidence: every excerpt was compared with `git show b2e387db:<path>`; the engine facts were
checked against `C:\Users\mikew\.taom-src\v1.5.3\`. The drift check was run on 2026-09-23 and
printed nothing. `E:/repos/wt-014-enlistment-session-scope` and branch `plan-014-*` do not exist yet.

**Verdict: NOT executable as written. One blocking defect.** Step 4 has a verification the plan
cannot pass, so an executor following it correctly hits a STOP and cannot finish.

## Blocking

### B1. Step 4 breaks `EnlistmentContainerWiringTests`, and the plan says it stays green (plan lines 88-90, 529-535, 948-951, 1017-1018, 1151, 1174-1179)

`EnlistmentContainerWiringTests.BuildContainer()` (test file `:28-44` at `b2e387db`) registers only
`IModLogger`, `ICoopSessionProvider`, `ICoopPresenceProvider`, `IPathService` and
`EnlistmentIoC.RegisterEnlistmentFeature`. Step 4 makes `ServiceMaintenanceService` depend on
`IEnlistmentWaitMenuPresenter`. That presenter's graph pulls in two services that
`RegisterEnlistmentFeature` does not register:

- `EnlistmentWaitMenuPresenter` -> `IEnlistmentDialogGateService` -> `EnlistmentDialogGateService(…, IPlayerContextAdapter playerContext, …)`.
  `IPlayerContextAdapter` (`Main/Adapters/IPlayerContextAdapter.cs`, namespace `TAOM.Adapters`) is
  registered only in `Main/Features/Siege/SiegeDefenseIoC.cs:12`.
- `EnlistmentWaitMenuPresenter` -> `IEnlistmentPlayerActionService` -> `EnlistmentPlayerActionService(…, IDutyOrchestrationService duties, …)`.
  `IDutyOrchestrationService` (namespace `TAOM.Features.Enlistment.Duties`) is registered only in
  `Duties/DutiesIoC.cs:21`, which `Main/IoC.cs:179` calls, not `RegisterEnlistmentFeature`.

No validated root reaches the presenter today, and that is why the 5 tests pass at `b2e387db`.
After Step 4, `container.Validate(typeof(IServiceMaintenanceService))`,
`Validate(typeof(EnlistmentBehavior))` and `Validate(typeof(EnlistmentBattleBehavior))` all reach
the two unregistered interfaces. `MaintenanceService_Resolvable_*`, `LifecycleBehavior_Resolvable_*`
and `BattleBehavior_Resolvable_*` then fail. This is [Likely], from static analysis of the
registrations; I did not run it.

The executor then meets the STOP at lines 1174-1179. That stop is safe, but the plan cannot be
completed. Production is not affected: the live container registers both services, and nothing
under the presenter depends on `IServiceMaintenanceService`, so there is no cycle.

**Fix for the plan:** add `TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs` to
Scope, to Step 4's edits and to Step 4's commit. In `BuildContainer()`, next to the `IPathService`
line, add the following with a one-line comment naming the chain:

- `container.RegisterInstance(Substitute.For<TAOM.Adapters.IPlayerContextAdapter>());`
- `container.RegisterInstance(Substitute.For<TAOM.Features.Enlistment.Duties.IDutyOrchestrationService>());`

Then update everything that counts or names the in-scope files:

- line 89 ("unchanged by this plan")
- line 533 ("DryIoc injects … by type"; true at runtime, false for this test container)
- the Step 4 commit file list at line 953
- Done criteria line 1157 (12 paths become 13)
- Step 4 Verify, which could assert the file diff

## Non-blocking

1. **Line 242-243.** The plan says `git grep -n "Invalidate()" b2e387db -- Main/Features/Enlistment/Content TAOM.Tests`
   "finds only the two definitions". It prints 5 lines: the two definitions,
   `ServiceMaintenanceServiceTests.cs:122` (`_status.Received(1).Invalidate()`),
   `ServiceStatusServiceTests.cs:320` and `MarriageClanPoolStampTests.cs:40`. The conclusion still holds:
   nothing calls the rhythm service's `Invalidate`, as the Step 0 `_rhythm` grep confirms.
2. **Line 1085, the dash check.** In this Git Bash, `$'\u2014'` expands to the literal 6 characters `\u2014`
   (checked with `printf '%s' $'\u2014' | od -c`). The grep can therefore never match, and the check prints `0`
   whatever the diff contains. Use a Python check such as
   `git diff -U0 … | python -X utf8 -c "import sys; print(sum(1 for l in sys.stdin if l.startswith('+') and ('\u2014' in l or '\u2013' in l)))"`.
3. **Lines 445-448.** The summary of `.claude/rules/csharp-architecture.md:96-114` is inexact. The rule says to
   call the reset "from `OnSessionLaunched` when [SyncData] did not [load]", and `LoadFrom` clears transients.
   The plan wires `OnNewGameCreated` instead. That hook is equivalent here (the engine fires it before
   `OnSessionStart` on a new campaign, verified in `Campaign.cs:1675-1712`), but the plan should say it
   departs from the rule's hook on purpose.
4. **Lines 1057-1060 (Step 7 doc edit).** The plan rewrites only `:1696-1699`. `docs/features/enlistment.md:1692-1695`
   keeps "Two guards, because they cover different paths" and "`ResetForNewSession` covers the load path", which is
   only half true after the change. The prose is readable but partly stale.
5. **Line 113.** It cites the excerpt as `:223-234`, but the block shows only `:223-231`. This is cosmetic.
6. **Line 894-896.** The CS0104 fallback asks the executor to improvise ("replace the two usings … with
   fully qualified …"). A type-name collision check across all Enlistment namespaces, `TAOM.Adapters`
   and `CoopInterop` found no duplicates, so CS0104 cannot occur at `b2e387db`. Make it a plain STOP.
7. **Lines 1028-1029.** The `Not-tested:` trailer should say it goes in its own `-m` paragraph. Similarly,
   lines 829-832 and 957-959 give each body as one prose sentence to "wrap at 72". A weak executor may pass
   it unwrapped and then fail the awk check at line 570. Give the pre-wrapped body text.
8. **Line 1146.** "failures a subset of …" is a comparison the executor makes by eye. Acceptable, but a
   `grep "Failed "` over the trx or console output would make it mechanical.

## Checklist results

- **Excerpts vs `b2e387db` (item 3):** every cited excerpt and line number matches, apart from the
  cosmetic range in non-blocking 5. Files checked:
  - `ServiceAttachmentService.cs` 29-44 and 223-231
  - `IServiceAttachmentService.cs` 25-37 and 55
  - `EnlistmentWaitMenuPresenter.cs` 39-44, 60-70, 118-160
  - `ArmyRhythmSnapshotService.cs` 7-13, 24-32, 34-65, 67-71
  - `ServiceMaintenanceService.cs` 6, 39-84, 206-241
  - `IServiceMaintenanceService.cs` 50-55
  - `EnlistmentBehavior.cs` 32-50, 112-130, 147 lines
  - `EnlistmentReconciler.cs` 49-52, 101, 450-456, 624-645
  - `docs/features/enlistment.md` 469-474, 1692-1701
  - `EnlistmentIoC.cs` 28, 31, 47, 55, 75, 90
  - `EnlistmentMenuBehavior.cs` 74-75
  - `MobilePartyAttachmentAdapter.cs` 161-164, 208; `ArmyMembershipAdapter.cs` 25, 147-153; `CommanderLordAdapter.cs` 74
  - `ServiceStatusService.cs` 148
  - `SubModule.cs` (via `git show`): 22 `AddBehavior(IoC.Resolve`, 34 `AddBehavior(new`, 1412-1421, 788-798
  - `IoC.cs` (via `git show`): 129, 178-179
  - `SubModule.xml` line 6 = `v2.0.30`
  - 6 instance `ResetForNewSession` implementations
  - the test files' usings, fields and constructor lines (`ServiceMaintenanceServiceTests` 1-7, 26, 44, 52, 164; `EnlistmentPumpAuthorityTests` 1-6, 49-53)
  - engine cache: `Campaign.cs:1675-1712`, `CampaignEvents.cs:861, 2098-2100`, `CampaignBehaviorBase.cs:12-15`
- **Test code compiles as designed:**
  - `IInquiryAdapter.ShowTwoOptionInquiry` has 14 parameters, matching the plan's `Received` shape.
  - The presenter constructor order matches.
  - `ArmyRhythmProbe` and `ServiceContentRecord` have public parameterless constructors.
  - `CoopAuthorityGateTests.cs:223-231` is a real precedent for calling a behavior hook directly.
  - The RED error codes (CS1061, CS1729 with 14 arguments, CS0122) are the ones the edits produce.
  - Nullable is enabled with no TreatWarningsAsErrors, so `OnNewGameCreated(null)` is a warning, not an error.
- **Every step ends in a command with an expected result (item 2):** yes. Step 4's expected result is wrong (B1).
- **TDD order (item 4):** RED before GREEN in Steps 1/2, 3/4 and 5/6.
- **Issue-first:** "create before implementation lands (orchestrator)".
- **ADRs named with summaries:** ADR-002/003/004/005/007/008 and the session-reset rule.
- **Single-owner files:** `IoC.cs`, `SubModule.cs`, `TAOM.csproj` and `Directory.Build.props` are "recommend, don't edit", and none needs a change.
- **STOP conditions:** specific to this plan's risks (hook denial reading the main index, worktree collisions, DryIoc cycle, TypeLoad on `CampaignBehaviorBase`).
- **Done criteria:** machine-checkable, with the exception noted in non-blocking 8.
- **Planned-at and drift check:** planned-at `b2e387db` is consistent with Scope; `TAOM.Tests/Features/Enlistment` covers every in-scope test file, including the one B1 adds.
- **Non-deploying commands:** every build and test command carries `-p:DisableModuleCopy=true -p:ModuleId=`, and `./build.ps1` is forbidden.
- **Commit subjects:** 70, 71, 65 and 67 characters, as the plan measured.
- **Dashes and secrets (item 5):** em dashes appear only inside verbatim code excerpts (lines 120, 158, 293, 298, 333, 343, 350), which is exempt. There are no secrets.
