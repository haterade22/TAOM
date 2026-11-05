# Input pack: WP 127 architecture-refactors

Title (draft): Architecture refactors that hold on 1.4.x: plans 021, 025, 026, the combat model split, partial services
Wave: 3   Lane: E   Draft priority: P3   Groups: 5   Port commits: 18
Depends on (WP ids): 105

## Orchestrator notes for this WP (binding)
- ADR and rule edits under docs/adrs/ are the maintainer's (config-protection); present exact diffs.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D1-plan-018-composition-root.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `plan-025-delete-unreachable-scaffolds`  P3  track=architecture  commits=3  sim: clean=0 conflict=3
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs
- `032481cc6` 2026-09-24 PORT P3 pick=conflict :: refactor(cache-rebuild): v2.0.30 - delete two unreachable scaffolds
  - why: Deletes IEditorSceneAdapter (no implementation, no reference) and the EditorCacheRebuild Caching scaffold, which is registered but never resolved; on 1.4.x its only references are two registrations (EditorCacheRebuildIoC.cs:16-17) and two reserved config properties (CacheRebuildConfig.cs:43,46).
  - port notes: All 19 files exist on 1.4.x. Only ReflectionSiteBindingTests.cs and reflection-sites.md drifted, where 1.4.x added Patch80 rows further down, so the PathReuseCache row removal applies at an offset. This is a deletion that keeps behaviour unchanged.
- `76f22e111` 2026-09-25 PORT P3 pick=conflict :: fix(cache-rebuild): v2.0.30 - review follow-ups for plan 025
  - why: Removes the permanent absence test and corrects editor-cache-rebuild.md (the Phase 1 path memoization claim, the unmeasured 2-3x figure) and the provenance note in reflection-sites.md.
  - port notes: Take the test removal and the two doc files. The doc sentence that Phase 2 pathfinds with a different cost multiplier cites v1.5.3: re-check it against the v1.4.8 dump before copying it. The records are optional.
  - prereqs: 032481cc6
  - conflicts: TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs
- `97344cf59` 2026-09-25 PORT P3 pick=conflict :: fix(cache-rebuild): v2.0.30 - convergence fixes for plan 025
  - why: Docs-only convergence fix to a lessons recurrence note and a CHANGELOG provenance sentence.
  - port notes: Only needed if plan 025's records and lessons are ported; the CHANGELOG hunk gets rewritten anyway.
  - prereqs: 76f22e111

### group `plan-021-architecture-rule-amendments`  P3  track=architecture  commits=9  sim: clean=2 conflict=7
- `1cdf8eb02` 2026-09-25 PORT P3 pick=clean :: docs(adr): v2.0.30 - record boundary seams, conditional interfaces
  - why: Amends ADR-002, ADR-007 and ADR-008 to accept protected-virtual boundary seams and conditional service interfaces; this documents code that already exists identically on 1.4.5 (40 protected virtual members in RefugeService there) and keeps 1.4.x reviews from flagging code ported from 1.5.x.
  - port notes: Clean pick: the ADR files are unchanged on 1.4.5 since the fork. ADR-007 cites plans/_audit/2026-09-23-opus/verify-a-batch-02.md, which does not exist on 1.4.x; cite the 1.5.x ref or drop the path. This is the rule basis for plan 026 (a39575410).
- `9c29d7324` 2026-09-25 PORT P3 pick=conflict :: docs(rules): v2.0.30 - align three architecture rules with the code
  - why: Brings the rule files, review reference, feature-builder agent, /new-feature and docs/ai-includes guides in line with the amended ADRs (seams, conditional interfaces, optional hook interfaces).
  - port notes: Expect conflicts beyond CHANGELOG. c79a58521 rewrote think-before-coding.md, AGENTS.md (its Architecture row does not exist on 1.4.5) and .ai/review-reference.md on 1.5.x, so carry the meaning by hand into the 1.4.x text. The .claude/skills/deep-review/lenses/1-standards.md hunk needs the lens files from fa8c06f82, which are absent on 1.4.5. Without them, put the same check into the Standards agent prompt of the 1.4.x deep-review SKILL.md.
  - prereqs: 1cdf8eb02
- `7d1b7a542` 2026-09-25 PORT P3 pick=conflict :: docs(adr): v2.0.30 - let seams call the engine, interface optional
  - why: Two ADR wording fixes from the plan 021 review: ADR-008 lets a seam make an engine call rather than only a static read, and ADR-002 step 4 creates a service interface only when a fake or a second implementation needs it.
  - port notes: ADR text only; apply after 1cdf8eb02.
  - prereqs: 1cdf8eb02
- `d4e6273a1` 2026-09-25 PORT P3 pick=conflict :: fix(rules): v2.0.30 - review follow-ups for plan 021
  - why: Review follow-ups that finish aligning the rules: the hook interface in think-before-coding.md, the seam exception in codex-verify and the improve audit playbook, and the architecture one-liner in the engine lifecycle doc; it also adds the review report, RCA and lessons.
  - port notes: The .ai/review-reference.md hunk swaps 'v1.5.2' for the pinned-game-version.txt reference; keep that version-neutral wording on 1.4.x. The lens hunk needs fa8c06f82. The deep-review-021 report, RCA and REVIEW-LOG entry are optional records; the lessons/misc.md append is worth keeping.
  - prereqs: 9c29d7324
- `e809f258c` 2026-09-25 PORT P3 pick=conflict :: fix(rules): v2.0.30 - convergence fixes for plan 021
  - why: Convergence fixes: architecture.md exempts value types from the seam-only rule, and the Phase 4 sample in decompiled-code-analysis.md resolves the concrete service it registers (DryIoc would throw on the interface and the sample's empty catch would swallow it).
  - port notes: Docs only. The CHANGELOG, REVIEW-LOG and review-record hunks are optional.
  - prereqs: d4e6273a1
- `b22edd473` 2026-09-25 PORT P3 pick=conflict :: docs(adr): v2.0.30 - three ADR lines the plan 021 review found
  - why: Fixes three one-line ADR contradictions: CampaignTime is a struct and leaves ADR-007's sealed list, ADR-008's checklist names the seam exception, and ADR-002's checklist accepts a test subclass that overrides seams.
  - port notes: ADR text only; the CHANGELOG hunk is bookkeeping.
  - prereqs: 7d1b7a542
- `16e45bf8e` 2026-09-25 PORT P3 pick=clean :: docs(reviews): v2.0.30 - Codex prompt for plan 021
  - why: Adds the Codex adversarial prompt for the plan 021 review as a record; it has no rule or code effect.
  - port notes: Optional record and a clean pick. Carry it only if the plan 021 review report from d4e6273a1 is carried.
- `6a2ce8cb3` 2026-09-25 PORT P3 pick=conflict :: docs(adr): v2.0.30 - seam helpers, seam debt, convergence fixes
  - why: Puts maintainer decisions 55 and 56 into ADR-007 (a private helper that only seams call is part of the seam body; the four seams holding decision logic are debt for plan 026) and removes ADR-008's grep CI step, which could not tell a seam from a violation.
  - port notes: ADR-007 now names plan 026 and its seams. That is accurate on 1.4.x only if a39575410 is ported; otherwise keep the seam-debt wording without the plan link. The lens hunk needs fa8c06f82. The review-record and REVIEW-LOG hunks are optional.
  - prereqs: b22edd473
- `ec5a19270` 2026-09-25 PORT P3 pick=conflict :: docs(reviews): v2.0.30 - close the plan 021 review records
  - why: Makes ADR-007 stop presenting the four plan-026 seams as a complete list (any seam that breaks condition 2 is a finding), and fixes the accuracy of the review records.
  - port notes: Take the ADR-007 hunk. The deep-review-021 report, RCA and REVIEW-LOG hunks are optional records.
  - prereqs: 6a2ce8cb3
skipped in this group: `9dcf1a4de` SKIP-BOOKKEEPING docs(changelog): v2.0.30 - rewrap the plan 021 entry, name b22edd47

### group `plan-026-seam-decision-logic`  P3  track=architecture  commits=4  sim: clean=0 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Refuge/RefugeService.cs, Main/Features/Refuge/WardenService.cs, TAOM.Tests/Features/FieldCamp/CampServiceTests.cs, TAOM.Tests/Features/Refuge/RefugeServiceTests.cs, TAOM.Tests/Features/Refuge/WardenServiceTests.cs
- `a39575410` 2026-09-25 PORT P3 pick=conflict :: refactor(seams): v2.0.30 - move decision logic out of nine seams
  - why: Moves TAOM decision logic out of nine protected-virtual seams in SupplyOrderService, RefugeService, WardenService and CampService into tested internal methods, with 76 new tests and no behaviour change.
  - port notes: The code applies cleanly: all four service files on origin/bannerlord-1.4.5 are byte-identical to this commit's parent (blobs 094c2285, 121a7474, e5416df6, 560e434a). Three of the four test files differ on 1.4.5 by one [TestCategory("RequiresGame")] line from b8c000452 (1.5.x CI), which does not block. It uses only engine APIs already used on 1.4.5 (Settlement.GetPosition2D and the like). It must travel with 12b1c2003, which fixes a defect this refactor introduced. Porting it first also eases later Refuge fixes that touch the same files (63630e8a0, 81851d83a).
  - prereqs: 1cdf8eb02
- `12b1c2003` 2026-09-25 PORT P3 pick=conflict :: fix(seams): v2.0.30 - review follow-ups for plan 026
  - why: Review follow-ups for the seam refactor: the refuge's per-row peace check again keeps a prisoner when the refuge or its faction is missing (the refactor had flipped it to release), the promotion seams act on the minted hero instead of a second lookup, the raid scan presizes its list, and the pure FortificationSearch tests move to their own file.
  - port notes: The simulated code conflicts exist only because a39575410 is not on 1.4.5; apply this after it. FortificationSearchTests was split out untagged so 1.5.x hosted CI would run it; ccef172ad removed CI on 1.4.x, so the reason is moot and the split is harmless. The review report, RCA and REVIEW-LOG are optional records; the three lesson appends are worth keeping.
  - prereqs: a39575410
  - conflicts: Main/Features/Refuge/RefugeService.cs, Main/Features/Refuge/WardenService.cs, TAOM.Tests/Features/FieldCamp/CampServiceTests.cs, TAOM.Tests/Features/Refuge/RefugeServiceTests.cs, TAOM.Tests/Features/Refuge/WardenServiceTests.cs
- `aeb04bc48` 2026-09-25 PORT P3 pick=conflict :: fix(seams): v2.0.30 - convergence fixes for plan 026
  - why: Fixes the accuracy of the plan 026 CHANGELOG entry, review report and RCA; no code or test changes.
  - port notes: Optional. Carry it only if the plan 026 review report and RCA from 12b1c2003 are carried.
  - prereqs: 12b1c2003
- `5685676e3` 2026-09-25 PORT P3 pick=conflict :: docs(reviews): v2.0.30 - plan 026 Codex prompt and RED-step note
  - why: Adds the Codex prompt for the plan 026 review and a note on the executor's RED-step deviation; docs only.
  - port notes: Optional record; carry it under the same condition as aeb04bc48.
  - prereqs: 12b1c2003
skipped in this group: `af6329eac` SKIP-BOOKKEEPING docs(plans): v2.0.30 - plan 026 and its two cold reviews; `8c60d134f` SKIP-BOOKKEEPING docs(plans): v2.0.30 - plan 026 merged

### group `issue-737-combat-model-split`  P3  track=architecture  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs, Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs, Main/Features/SignatureStrikes/Hooks/StrikeContextFactory.cs, Main/SubModule.cs, TAOM.Tests/Features/RaceAbilities/RaceAbilitiesWiringTests.cs
- `f7e9bd0d2` 2026-10-05 PORT-PARTIAL P3 pick=conflict :: refactor(combat): v2.0.34 - damage model glue moves to feature hooks
  - why: ADR-002 refactor that moves per-feature glue out of the shared campaign damage model; the 1.4.5 TaomCombatMechanicsModel is 230 lines against the 150 cap, so the split applies there in a smaller form.
  - port notes: Redo the split on the 1.4.5 model, which holds CombatMechanics plus Refuge only: CombatMechanicsHooks (crush-through, charge knockdown, shield penetration, creature combat), RefugeDamageHooks, the line-ceiling test and the container wiring tests. Leave SignatureStrikeVerdicts, RaceAbilities wiring and the #610 charge-gate moves (post-fork features). No behaviour change intended. This split's review follow-ups are in bb68eeca3.
  - conflicts: Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs, Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs, Main/Features/SignatureStrikes/Hooks/StrikeContextFactory.cs, Main/SubModule.cs, TAOM.Tests/Features/RaceAbilities/RaceAbilitiesWiringTests.cs
  - issues: 610, 737

### group `adr-003-partial-services`  P3  track=architecture  commits=1  sim: clean=1 conflict=0
- `583585818` 2026-10-21 PORT-PARTIAL P3 pick=clean :: docs(adrs): v2.0.35 - ADR-003 allows a service split by concern
  - why: Amends ADR-003 to allow a hand-written service split into partial files by concern under stated conditions; the rule fits either line, and 1.4.x already has such a class (VolunteerRecruitmentService with 16 culture parts).
  - port notes: Take the amendment and the README row. Replace the FireAndTributeService example and the gap list with 1.4.x facts: HiddenHostsService, CreatureSiegeRules, FarWatchService and the MissionPerf hooks exist only on 1.5.x. Keep the gap noting that VolunteerRecruitmentService does not list its parts in its remarks. The pick is otherwise clean.
  - issues: 870

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 7, batch 8, batch 14, batch 22
