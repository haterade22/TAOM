# Input pack: WP 119 memory-cross-campaign-resets

Title (draft): Reset per-campaign state on every new campaign and find what a second game keeps (plans 001 and 014)
Wave: 2   Lane: C   Draft priority: P1   Groups: 3   Port commits: 14
Depends on (WP ids): 105

## Orchestrator notes for this WP (binding)
- Closes the 1.4.x TODO plan 001 (its CareerSystem half is already on 1.4.x as f4273639; port the SpecialResources half 4263535a1, 8ea315052, 0935533a6).
- Plan 014 series (DECISIONS #28 lists it): a67792c40 adds one ResetSessionCaches line in the SubModule.cs OnGameEnd block.
- Take 6ae924638's HeroRace _isNewCampaign hunk with eb5cf0289, and eb6d18431.
- V07: these are correctness fixes; on 1.5.x the managed retention persisted after them. Add a heap-snapshot procedure (what to capture, how to compare) for MEMORY-05; share the probe with WP114.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V07-memory.md
- plans/_audit/2026-11-04-port148/verify/V01-save-growth.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- MEMORY-05 [P2/medium] A second game in one process keeps much of the first: +1.8 GB, and the managed heap often stays at 200 to 450 MB at the menu | port: e99d705a3,a67792c40,4263535a1  (report: plans/_audit/2026-11-04-port148/deepdive/memory.md)

## Port groups owned by this WP (from the commit classification)

### group `plan-014-enlistment-session-scope`  P1  track=shared-feature-fix  commits=9  sim: clean=2 conflict=7
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs, Main/Features/Enlistment/EnlistmentReconciler.cs, Main/Features/Enlistment/IServiceAttachmentService.cs, Main/Features/Enlistment/IServiceMaintenanceService.cs, Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs, Main/Features/Enlistment/ServiceAttachmentService.cs, Main/Features/Enlistment/ServiceMaintenanceService.cs, TAOM.Tests/Features/Enlistment/EnlistmentReconcilerTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentStopEndTests.cs, TAOM.Tests/Features/Enlistment/SettlementFollowingTests.cs
- `6fb37c9f7` 2026-09-24 PORT P1 pick=clean :: fix(enlistment): v2.0.30 - reset the dwell anchor, offer latch, rhythm
  - why: Adds ResetForNewSession to Enlistment's dwell anchor, arrival-offer latch and rhythm snapshot, absolute-clock fields on process-lifetime singletons that otherwise leak into a reloaded or second campaign; the same code is on 1.4.x unchanged since the fork.
  - port notes: Picks clean (Main/Features/Enlistment untouched on 1.4.x since a4c6d7c30). Adds the methods only; wiring follows in f6ac2d335 and e99d705a3. Port 6fb37c9f7..aac0e05bf together, ideally squashed to the final state.
- `f6ac2d335` 2026-09-24 PORT P1 pick=clean :: fix(enlistment): v2.0.30 - ResetSessionCaches drops the session latches
  - why: Calls the three new resets from ServiceMaintenanceService.ResetSessionCaches, the feature's single reset point, and updates the container wiring tests.
  - port notes: Picks clean after 6fb37c9f7.
  - prereqs: 6fb37c9f7
- `e99d705a3` 2026-09-24 PORT P1 pick=conflict :: fix(enlistment): v2.0.30 - reset session caches on a new campaign
  - why: Wires ResetSessionCaches to OnNewGameCreated, because a new campaign never fires OnGameLoaded (verified in the v1.4.8 dump, Campaign.DoLoadingForGameType, Campaign.cs:1663-1694), so cached commander and army handles leaked into a second campaign.
  - port notes: The test-file conflict is only the missing prerequisite (6fb37c9f7 creates EnlistmentSessionResetTests.cs). EnlistmentBehavior.cs is unchanged on 1.4.x; its OnGameLoaded already calls ResetSessionCaches (EnlistmentBehavior.cs:120).
  - prereqs: 6fb37c9f7, f6ac2d335
  - conflicts: TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs
- `74fb3d35e` 2026-09-24 PORT P3 pick=conflict :: docs(enlistment): v2.0.30 - session reset now covers a new campaign
  - why: Updates reconciler comments, the IServiceMaintenanceService doc and docs/features/enlistment.md to say a new campaign now reaches ResetSessionCaches.
  - port notes: Comment and feature-doc changes; enlistment.md is identical at fork and on 1.4.x, and the 1.4.x EnlistmentReconciler.cs:453 still carries the stale comment this fixes. The conflict is the prerequisite only.
  - prereqs: f6ac2d335, e99d705a3
  - conflicts: Main/Features/Enlistment/IServiceMaintenanceService.cs
- `b9d18458e` 2026-09-24 PORT P1 pick=conflict :: fix(enlistment): v2.0.30 - review follow-ups for plan 014
  - why: Review follow-ups: the attachment service's session reset also drops the adapter's cached commander party (matched by StringId, which a later campaign can reissue), the service-level InvalidateCommanderCache pass-through goes, and new tests pin the load and new-campaign reset order.
  - port notes: Code conflicts are series prerequisites only. DischargeService keeps calling InvalidateCommanderCache on its adapter field (_attachment is IMobilePartyAttachmentAdapter, origin/bannerlord-1.4.5 DischargeService.cs:12), so removing the service-level method breaks nothing. Review record, Codex prompt, RCA and lessons optional; drop the CHANGELOG hunk.
  - prereqs: e99d705a3, 74fb3d35e
  - conflicts: Main/Features/Enlistment/IServiceAttachmentService.cs, Main/Features/Enlistment/IServiceMaintenanceService.cs, Main/Features/Enlistment/ServiceAttachmentService.cs, Main/Features/Enlistment/ServiceMaintenanceService.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs
- `41754a03e` 2026-09-24 PORT P3 pick=conflict :: fix(enlistment): v2.0.30 - convergence fixes for plan 014
  - why: Text-only convergence fixes that name exactly what the reset clears and list the two singleton fields it does not (CommanderLordAdapter's MapEvent cache, BattleMeritAccumulator._pending).
  - port notes: Interface doc comment, test comment and docs only; conflicts are series prerequisites.
  - prereqs: b9d18458e
  - conflicts: Main/Features/Enlistment/IServiceMaintenanceService.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs
- `a67792c40` 2026-09-24 PORT P1 pick=conflict :: fix(enlistment): v2.0.30 - apply maintainer decisions for plan 014
  - why: Applies the maintainer's plan 014 decisions: the commander-loss modal latch clears on reset and on every discharge, every co-op peer resets on load above the authority gate, a save with no Enlistment data clears the store, the shore-leave latch clears when the stop ends, and OnGameEnd calls ResetSessionCaches.
  - port notes: Main/SubModule.cs is single-owner: add the one ResetSessionCaches line inside the existing best-effort teardown block at origin/bannerlord-1.4.5 Main/SubModule.cs:691-694 (1.5.x had it near line 794). DischargeService.EnlistmentEnded exists on 1.4.x (DischargeService.cs:45). Other conflicts are series prerequisites; drop the CHANGELOG hunk.
  - prereqs: b9d18458e, 41754a03e
  - conflicts: Main/Features/Enlistment/EnlistmentReconciler.cs, Main/Features/Enlistment/IServiceMaintenanceService.cs, Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs, Main/Features/Enlistment/ServiceMaintenanceService.cs, TAOM.Tests/Features/Enlistment/EnlistmentReconcilerTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs
  - issues: 656
- `fd61757bf` 2026-09-24 PORT P1 pick=conflict :: fix(enlistment): v2.0.30 - review follow-ups for plan 014
  - why: Repairs a defect a67792c40 introduced (the shore-leave latch cleared only in the exit sweep, which shore leave suspends) by ending the stop on the commander's settlement-left edge in EnlistmentMaintenanceBehavior, with wiring and co-op load tests.
  - port notes: Must travel with a67792c40. The game-end source-presence test scans SubModule.cs from OnGameEnd to OnGameStart; that order holds on 1.4.x (lines 668 and 697). Review record, prompt and RCA optional; drop the CHANGELOG hunk.
  - prereqs: a67792c40
  - conflicts: Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs, Main/Features/Enlistment/EnlistmentReconciler.cs, Main/Features/Enlistment/IServiceAttachmentService.cs, Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs, Main/Features/Enlistment/ServiceMaintenanceService.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs, TAOM.Tests/Features/Enlistment/SettlementFollowingTests.cs
  - issues: 656
- `aac0e05bf` 2026-09-24 PORT P2 pick=conflict :: fix(enlistment): v2.0.30 - convergence fixes for plan 014
  - why: Convergence fixes: EnlistmentStopEndTests now pins the OnSettlementLeftEvent subscription (a mutant deleting it stayed green before), plus review-doc corrections.
  - port notes: Test and review-doc changes only; last plan 014 commit before its merge 7feeec94f (#656).
  - prereqs: fd61757bf
  - conflicts: TAOM.Tests/Features/Enlistment/EnlistmentStopEndTests.cs
skipped in this group: `d1221b7fe` SKIP-BOOKKEEPING docs(changelog): v2.0.30 - enlistment session reset (plan 014)

### group `plan-001-cross-campaign-resets`  P1  track=shared-feature-fix  commits=3  sim: clean=0 conflict=3
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/SpecialResources/SpecialResourcesBehavior.cs, TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs
- `4263535a1` 2026-09-24 PORT P1 pick=conflict :: fix(specres): v2.0.30 - a new campaign no longer keeps the old balances
  - why: Special resource balances live in a process singleton (SpecialResourcesIoC.cs:11), so a second campaign in one process keeps the first one's balances and its first save persists them; 1.4.x still seeds the SyncData load from live storage (SpecialResourcesBehavior.cs:105) and never wipes it in OnNewGameCreated (:116-130).
  - port notes: Since the fork, 1.4.x changed this file only in RegisterEvents (1b00d1acd, the ScreenManager subscriber replacement), away from the SyncData and OnNewGameCreated hunks, so expect a clean or offset apply plus the CHANGELOG conflict. The CareerSystem half of plan 001 is already on 1.4.x via f4273639. Save-compat: no new save fields.
- `8ea315052` 2026-09-25 PORT P1 pick=conflict :: fix(specres): v2.0.30 - review follow-ups for plan 001
  - why: Review follow-ups to the plan 001 fix: comment-only edits in SpecialResourcesBehavior, corrected tests (a fake data store that records null and throws on a duplicate key, as BehaviorSaveData does) and a known-limitation note in the feature doc.
  - port notes: Land the comment and test changes with 4263535a1, plus special-resources.md; the records are optional. The no-record load gap it documents is still open on both lines.
  - prereqs: 4263535a1
  - conflicts: Main/Features/SpecialResources/SpecialResourcesBehavior.cs, TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs
- `0935533a6` 2026-09-25 PORT P3 pick=conflict :: fix(specres): v2.0.30 - convergence fixes for plan 001
  - why: Docs-only convergence fix: corrects the RCA's finding count and appends a convergence section to the review record.
  - port notes: Only needed if plan 001's review records are ported.
  - prereqs: 8ea315052

### group `hero-race-session-reset`  P1  track=diagnostics  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs
- `eb6d18431` 2026-10-27 PORT P3 pick=conflict :: fix(hero-race): v2.0.35 - a new game no longer warns of a pre-TAOM save
  - why: Tests for the 6ae924638 change that makes a new campaign log an information line instead of a misleading pre-TAOM-save warning; the 1.4.x RacePersistenceService.RestoreHeroRaces still warns on every empty map.
  - port notes: Take both test hunks; requires the RacePersistenceService hunk of 6ae924638 (the _isNewCampaign flag set in ResetForNewCampaign and cleared on load). Expect a context conflict in RacePersistenceServiceTests.cs. Neither player log shows the warning (both load saves), so this is log hygiene only.
  - prereqs: 6ae924638
  - conflicts: TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs
- `eb5cf0289` 2026-10-27 PORT P1 pick=conflict :: fix(hero-race): v2.0.35 - old saves no longer take the last game's races
  - why: Stops a save without race data, loaded after another campaign in the same session, from inheriting that campaign's hero races; the precondition holds on 1.4.x because v1.4.8 LoadBehaviorData calls SyncData only for a behavior the save holds (CampaignBehaviorDataStore.cs:86-106) and the 1.4.x clear runs only inside SyncData.
  - port notes: Take all seven files (RacePersistenceWiringTests.cs is new). ResetForNewSession also clears _isNewCampaign, a field the 6ae924638 HeroRace hunk adds: port that hunk first or drop the line. The 1.4.x behavior is built with new in SubModule.RegisterProgressionAndIdentity (SubModule.cs:853), so the constructor reset fits and the wiring test's SubModule.cs assertion holds. That test uses the 1.5.x-only RepoPaths.ReadSource: port TAOM.Tests/Infrastructure/RepoPaths.cs alone (from the 35007dbb8 umbrella) or use a local FindRepoRoot. Expect a context conflict in RacePersistenceServiceTests.cs.
  - prereqs: 6ae924638, 35007dbb8
  - conflicts: TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs
  - issues: 1035

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 6, batch 7, batch 29
