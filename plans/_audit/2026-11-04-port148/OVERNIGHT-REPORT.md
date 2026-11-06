# Overnight run report, 2026-11-05

This report grew with the run. The first 19 work packages are in the sections up to "In-game checks owed"; every later merge, the second integrated review, the verification at the tip and the symptom map are under "After the first report" and the sections that follow it. All of it is on the local branch `port148/overnight`. The trunk `bannerlord-1.4.5` did not move and nothing was pushed. **No line of this has run in the game yet**: every result below comes from builds, unit tests, binding tests and reviews.

## Work packages

| WP | Executor | Final review | Fix round | Merge | Tests after merge | Conflicts resolved |
|---|---|---|---|---|---|---|
| 104 | DONE | PASS | no | `e179ae138` | 9190 | 0 |
| 105 | DONE | PASS | no | `983bdf8f3` | 9225 | 0 |
| 106 | DONE | PASS | no | `d72c30727` | 9287 | 0 |
| 107 | DONE | PASS | no | `fed82a47c` | 9615 | 0 |
| 108 | PARTIAL | PASS | no | `b16c846d2` | 9295 | 1 |
| 109 | DONE | PASS | no | `3a6ad12bc` | 10169 | 3 |
| 110 | DONE | PASS | no | `e12a71f5e` | 9411 | 0 |
| 111 | DONE | PASS | no | `ea6e02bba` | 10385 | 4 |
| 112 | DONE | PASS | no | `2f524c519` | 9795 | 0 |
| 113 | DONE | PASS | no | `8cc1b9cc5` | 10262 | 4 |
| 114 | DONE | PASS | yes | `6ab12f4f9` | 9481 | 0 |
| 117 | DONE | PASS | no | `066f72428` | 11597 | 9 |
| 118 | DONE | PASS | no | `b6cd14309` | 10956 | 10 |
| 119 | DONE after the fix round | FIX (merge-time only) | yes | `af33aaace` | 11930 | 5 |
| 120 | DONE | PASS | no | `cb32ec0d6` | 10609 | 7 |
| 123 | DONE | PASS | no | `c09f5876f` | 11751 | 0 |
| 124 | DONE | PASS | no | `9a36aa7a5` | 11881 | 0 |
| 125 | DONE | PASS | no | `7d764cd78` | 12174 | 0 |
| 126 | DONE | PASS | no | `5fc3fa1fe` | 9425 | 0 |

WP108 is PARTIAL: its validator gates (Step B4) touch `.claude/`, which D25 reserves until WP102 and WP103 land; the runtime guard (Patch90) is merged. WP119's executor stopped on a mispredicted compiler code (CS1061, not CS0122); the fix round corrected the plan and finished the WP. Its final review verdict was FIX for merge-time items only (a stale rename, a changelog conflict, a stale doc bullet); the integrator applied all three in the merge commit.

## Integrated review

Five reviewers read the whole merged range through different lenses (merge resolutions; Harmony and wiring; threading and freeze safety; saves, settings and co-op; gates and docs), and a skeptic tried to refute each finding. No BLOCKER or MAJOR survived. Minor findings that survived:

- **MINOR** (`docs/features/coop-interop.md:317`): The excluded-settings count is stale. It says 'The 83 excluded (total minus relevant)', but the same paragraph now gives 271 total and 183 relevant, so the count should be 88. WP125 updated the total and the BattleLoadDiagnosticsSettings split and left this number at WP123's value. This is documentation only, with no runtime effect. The WP125 ledger entry already records it as a deferred doc fix, and no later commit fixes it.
- **MINOR** (`Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs:44 (stale siblings: ExitStallSampler.cs:1`): The Enable Exit Stall Sampler MCM hint says that turning it OFF keeps the other diagnostics "without any thread suspension". That became false in this range. MissionTickStallWatchdog and MainThreadFreezeWatchdog both default on, and both reach Thread.Suspend through ThreadStackCapture.Capture. A player who turns the exit sampler off to remove the mid-GC suspension risk still has two suspending samplers running, with no warning. The risk only arises after a thread has already been stuck for 10s or more, and the freeze sampler also skips the stack while the heap is moving, so MINOR is right. The Battle Freeze Sampler hint (:49) does disclose suspension and the same risk. The Main Thread Freeze Sampler hint (:54) says only "photographs the main thread's call stack when that is safe"; that wording is verbatim from plan 113 line 732, so it departs from no plan, but it never mentions suspension. Two code comments and two doc lines still call the exit sampler the only component that suspends the main thread. ThreadStackCapture's summary names two callers when there are three. The same hint is still present at the 1.5.x pin 4d1a9105a, which shows where the wording came from but does not make it true here.
- **MINOR** (`Main/Adapters/HeroRosterAdapter.cs:18 (GetAllHeroRaces), :26 and :32 (FindFirst lookups); docs/features/hero-r`): Regression from WP126 (496a68fdd) at tip 659671d9e. Moving the roster from Hero.AllAliveHeroes to Hero.FindAll adds dead heroes but stops covering a living hero whose CharacterObject is unregistered (a hollow character). That hero's race is no longer captured or restored, and CaptureHeroRaces drops his entry from the map, including at the session-launch capture, so it is lost at the next save. Afterwards he stays on the unsaved default race unless a birth triggers the WP112 repair, which copies the XML twin's race rather than any saved race. hero-race.md:32 says the roster covers 'alive, dead, disabled', which is untrue for this hero. Blast radius is limited: one hero's race, on a character that is already broken. How often this occurs is unmeasured; the origin of hollow characters is documented as unverified, though the Patch83 census (ReportHollowHeroCharacters) would log a count.
- **MINOR** (`Main/Features/TaomSettings.cs:802-803 (ApplyRecommendedBattleSettingsAction catch)`): The MCM Apply button's last-resort failure text is a literal English string with no {=key}, against plan 111's rule that new text is {=key}default registered in taom_module_strings.xml. It reads in English in all 12 languages. Narrow path: BattleSettingsAdviceNotifier.Apply already catches advisor failures and shows the localized taom_bc_advice_failed, so this catch fires only if IoC.Resolve throws or the notifier's own logging or display throws.
- **MINOR** (`plans/README.md:23`): Old plan 003's row still reads TODO even though both WPs that finish it have merged. plans/README.md:227-228 makes the integrator set an old row to DONE when its finishing WP merges. Correction to the finding: the DONE text at plans/123-always-on-cost-cuts.md:1159 should not be copied verbatim. It says PERF-01 goes to WP124, which plans/124-mission-hot-paths.md:1230 and README:234 and :247 contradict (PERF-01 is not ported).
- **MINOR** (`plans/README.md:290 and plans/README.md:320`): The baseline's 2251 warnings does not reproduce from a clean export of the same commit. As a result the WP104 record `2358 warnings (was 2251)` reads as a rise of 107, while clean exports show WP104 lowered the count by 7. No gate is affected, because plans/101-baseline-sync.md:555 says the count is 'not a gate'. The cost is a misleading record, so MINOR stands.
- **MINOR** (`docs/features/map-perf-profiler.md:28; docs/features/map-perf-profiler.md:367; docs/features/load-time-stamps.`): Narrowed from the original finding. Only these three added citations name an in-repo path that is absent on this line and carry no 1.5.x qualifier: `docs/migration/v1.5.2-impact.md`, `docs/reviews/deep-review-039-campaign-map-frame-profiler-2026-10-02.md` and `plans/040-load-time-stamps.md`. A reader looks here, finds nothing and is not told where the file lives. The plans' own convention is to label such paths `on the 1.5.x line`.

The doc-only ones were corrected in the commit that adds this report (old plan 003 row, the coop-interop excluded count, and the three 1.5.x citations). The WP125 registry wording was corrected there too. Still open at this report: the stale warning baseline note, the Exit Stall Sampler hint, the hollow-hero race regression and the literal English failure text. All four are fixed on `port148/integration-fixes`.

## Deferred, per work package

### WP104

- Step 6 (deep-review SKILL.md check 9) was NOT applied. The precondition says `grep -n "_harmony.PatchCategory" .claude/skills/deep-review/SKILL.md` must list exactly four lines (:365, :366, :367, :377), otherwise skip and report. It lists five: :362, :365, :366, :367, :377. The file has no drift since 1b00d1acd (`git diff --stat` is empty). The plan appears to have miscounted, because its replacement block already rewrites :362. Consequence: check 9 still tells reviewers to grep `_harmony.PatchCategory("<category-name>")`, so a /deep-review that follows it literally would flag every patch class as HIGH dead code. The independent reviewer and the integrator should know this. To finish it: apply the Step 6 block and commit message from the plan, then expect the plan's Verify values of 1 and 6.
- No step was deferred by D25's data rule. Step 4 staged only the five taom_patch_apply_* string rows (strings data, no item, roster, equipment, troop or mesh reference), and the validator ERROR count stayed at 3768, the same as Step 0.

### WP105

- Step 12 /deep-review was not run: the task says to skip it, and this session cannot invoke skills. The independent reviewer runs next; the probe list is in the plan's Maintenance notes.
- ADR for the composition root: maintainer via /new-adr (plan Maintenance notes; docs/adrs is maintainer-only). D2 is the decision of record until then.
- No step was deferred under the D25 data rule: this WP stages nothing under Main/_Module/ModuleData.

### WP106

- Step B10 /deep-review: skipped per the task; an independent reviewer runs next. Reviewer probes are the plan's 'What a reviewer should probe' list. The executor only grep-checked probes 5 and 6.
- No /review-codex or /codex-verify run (paid, maintainer only).
- No steps deferred by D25's data rule: this WP stages nothing under Main/_Module/ModuleData.

### WP107

- Step Z /deep-review: skipped per the task; this session cannot invoke skills and an independent reviewer runs next. No review follow-up commit exists.
- No step was deferred by D25's data rule: this plan stages nothing under Main/_Module/ModuleData.
- Plan's own deferred items, unchanged: V04's void Priority.First finalizer on TickAgentsAndTeamsImp (FREEZE-THREADING-01 fix (b), agent-tick half; new work, orchestrator decides); an in-game per-call cost measurement; a mission-tick dev-trigger run after WP106 is verified; whether TAOM's own Harmony owner should be protected.
- Lessons entries the ported RCA names (docs/reviews/lessons/harmony-il.md and build-tooling-workflow.md) arrive with WP142, not here.
- Tracking issue for new work 107-2 and the Part C test seam does not exist and was not created (executors never create issues). The plan's commit messages carry no Refs line, so nothing was left out; the integrator creates it.

### WP108

- Step B4 (all of it): MISSING_COLLISION_BODY, MISSING_VISUAL_MESH and COLLISION_BODY_BORROWED in tools/validate_moduledata.py, the taom_schema.py Path fix, tools/blender/add_collision_body.py and tests, test_collision_body_borrowed.py, the hook codes, the rule rows, the moduledata-validation, weapon-workflow and ranged-ladders docs, and the #633 RCA. Blocked by the STOP above, not by the data rule.
- Step B5 (audit report docs/audits/armory-ref-audit.md, troll allowlist decision, docs/audits/README.md row): deferred under D20 and D25 rule 2. Step B0 recorded NOT PUBLIC: LOTRLOME_Armory v2.0.26, elk and animalia monster files False, ladder_gondor_bow_t4 False, cooked packs True, loose tree False. KNOWN_DEAD_MESHES keeps its three troll entries.
- Body and mesh verdicts against the public Armory: owed. None were measured. MISSING_COLLISION_BODY, COLLISION_BODY_BORROWED and MISSING_VISUAL_MESH counts and the warm validator time do not exist because B4 did not run.
- Part B done criteria that depend on B4: Ran 265 tests, the two hook codes, the validator exit on the public Armory, and tool suite +51 (measured +19: 1501 to 1520).
- Step C1 item 3: wp108-scripts cleanup waits for the integrator's merge and B4.

### WP109

- No step was deferred by D25's data rule. The only ModuleData staged is string rows: taom_module_strings.xml plus English rows in the 12 language files. validate_moduledata showed 0 new ERROR lines against the Step 0.5 file, which is measured on the provisional v2.0.26 Armory.
- Step D /deep-review skipped as instructed; the independent reviewer should probe the plan's 'Review spots': the try/finally (no catch, one closer, moved body unchanged), module order and the MainMenu install with its MCM fallback, the two LoadInstalled adaptations, the SaveShield claims in docs and the trigger hint, the licence block, and no Patch103 string or watch behaviour hand-wired in SubModule.cs. /review-codex and /codex-verify not run (paid, maintainer only).
- Deferred on purpose by the plan: translations of the two new strings (WP135, /localize, paid; the 12 files carry English rows until then).
- Deferred on purpose by the plan: per-registration isolation in AddTaomBehavior (open on both lines); guards for the engine's other unbounded per-frame pools (unmeasured).
- Left to later WPs by the plan: engine docs including mission-frame-threads-and-native-costs.md (WP117) and the lifecycle doc (WP144); the native-crash-triage skill paragraph and .ai/review-reference.md (WP143); the harmony-patches.md AddTaomBehavior paragraph (WP111/WP143); NameplateCull, MapViewRelease and their licence and provenance extension (WP120); the full TickProfileLines.cs (WP117); binding-gate runsettings and the three-argument ResolveGameDir (WP122).
- Skipped as out of plan scope: CHANGELOG, feature-map, INDEX, tools/README, lessons, and the 649cc30ff Diag699 tracing hunks.
- No maintainer-only file change is needed: nothing in .claude/settings*.json, Directory.Build.props or docs/adrs.

### WP110

- Nothing was deferred under D25's data rule. C4 staged ModuleData string files only, and the validator stayed at the provisional baseline (3768 errors, all BROKEN_ITEM_REF; 120 warnings). No item or mesh verdict was made.
- /deep-review skipped as instructed; the independent reviewer runs next. The plan's review focus: A5's adapted PlayerFought (main party side against capturer side; an enlisted parked party gets no credit), C3's SubModule construction (argument order matches the constructor), C2's both-sides start (an already started one-sided war stays started), Part B's hint texts and that no TaomSettings default moved.
- Translating taom_wotr_victory_lead is left to WP135 and costs money: the row is English in all 12 languages.
- GitHub issues: not created. Part C's new work (WOTR-03, WOTR-04, WOTR-06/10 diagnostics, WOTR-08) has no issue number; the integrator creates it. No commit carries a Refs line for it.
- Other plans own these: the WP122 test conversions onto EngineDataStore, the WP123 allowlist entry deletion, the WP129 rebase onto A5, and the WP142 lessons and WP143 rule reconciliation. See Maintenance notes in the plan.

### WP111

- Plan's own hand-offs, none on this branch: feature-map rows for BattleCorpses and StuckBattle, docs/INDEX.md, and the lessons entries (state-lifecycle-save.md #606, adapters-taleworlds-api.md #699 and #748, build-tooling-workflow.md, localization-ui.md, campaign-mechanics.md, misc.md) go to WP142.
- harmony-patches.md and adapters.md reconciliation with the 1.5.x tip goes to WP143; the deep-review lifecycle line goes to WP138.
- Translating the six taom_bc_advice_* strings goes to WP135 (paid, maintainer). They are English seed rows in all 12 languages for now.
- /deep-review was skipped per the task; an independent reviewer runs next.
- No step was deferred under D25's data rule: the only ModuleData staged is strings (taom_module_strings.xml and the 12 language files), with no item, roster, equipment, troop or mesh reference. Validator errors are unchanged at 3768 BROKEN_ITEM_REF (provisional baseline, v2.0.26 Armory).

### WP112

- No step was deferred under the D25 data rule: this WP touches no ModuleData.
- Going to later WPs per the plan's Disposition table: the two SupplyNativeQueryTests IL-fingerprint tests (WP118, needs IlCallScanner.Fingerprint); tools/ingame_checks/1076.toml and its verdict fixtures (WP139); patch-targets.md rows, the .ai/verification.md api-snapshot row and verify SKILL.md Step 2d (WP137); lessons and LESSONS-LEARNED (WP142).
- Not ported, per the plan: the 1.5.x RCAs, review records and Codex prompts; the 6ae924638 hunks owned by WP119, WP123 and WP129; 84f0459bc (D12).

### WP113

- GitHub issue for the new work (freeze sampler, render-wait notice, stale-notice evidence, opt-in dump, shader-cache line) does not exist: the Refs #<n> trailers on the Part C, D and E commits were left out. A1 keeps its Refs #1025. The maintainer or orchestrator creates the issue.
- /deep-review on Parts C and D and /verify before each part's final commit were not run: this session cannot invoke skills. An independent reviewer runs next; the integrator owns /verify.
- Translation of the ten new keys (taom_shader_notice_* 3, taom_rw_notice_* 4, taom_bld_stall_* 3): English placeholder rows only, WP135 or /localize (paid).
- feature-map.md, lessons files and the other bookkeeping docs: WPs 142 to 144, as the plan says. feature-map.md has no diff.
- Plan's deferred items are untouched: LogTailCollector rgl selection, ShaderPrecompilePlayerAgentGuard FinishDeployment inside OnMissionTick, the precompile hint strings that still say reordering, and the polls=1 means #352 claim at the sites plan 108 did not hand over.
- No step was deferred by D25's data rule. Every ModuleData change is a strings row, and validate_moduledata.py printed 3768 errors and 120 warnings (BROKEN_ITEM_REF 3768), the same as the provisional baseline.

### WP114

- Nothing was deferred under the D25 data rule. The only ModuleData commit (863cfbd98) touches two config files, initial_child_generation.json and race_age_config.json, with no item, roster, equipment, troop or mesh reference. The validator showed no new ERROR.
- The Step A1 check `grep -c 'public 2026-09-29'` printed 0. I treated it as waived by D25 rule 3 because the data rule allows this step. Re-run the validator after the public Armory is installed.
- The Maintenance-notes hand-offs stay with their owners: WP142 for lessons (see handoff), WP143 for the CLAUDE.md patch category recount, WP144 for the save-cost section, WP139 for the [Session] summary.

### WP117

- /deep-review (Step B5): skipped on orders, an independent reviewer runs next. Reviewer probe list is in the plan's Maintenance notes. Not run, not claimed.
- A2.1 item 3 skipped, as the plan directs: the precondition grep printed 1, not 0, because WP107's `IsExcludedTargetMethod_MissionOnTickOnPreTickAndSpawnAgent_ReturnFalse` already exists. PatchShieldPolicyTests is unchanged, so `IsExcludedTargetMethod_MissionTickTargetsKeptOffTheShield_ReturnTrue` did not land. The comment copied into PatchShieldPolicy.cs says PatchShieldPolicyTests pins the split without the game, but only the negative half is pinned there; the positive half is pinned only by HitchProbeBindingTests against the installed engine. Reviewer or integrator: decide whether to add that one test.
- No D25 data-rule deferral applied: this plan stages no `Main/_Module/ModuleData`.
- Not landed here, owners per the plan: `af990ca76`'s inventory-memory corrections (`docs/features/battle-load-diagnostics.md` to WP120 Part A3; `docs/reviews/rca-memory-instruments-2026-09-12.md` and `docs/investigations/native-commit-audit-2026-08.md` to WP142).
- Later WPs per the plan: map profiler menu category (WP125), `perf_runs.py` extensions (WP138), `patch-targets.md` regeneration and `native_decompile.py` (WP137), lessons, feature-map, README and CLAUDE.md count syncs (WP142 to WP144). `[InvOpen]` goes to WP120. A v1.4.8 check that the clip-list walk is safe while a clip loads needs WP137's tools.
- GitHub issue: no Refs line used; the plan names only existing 1.5.x issues (#608, #710, #711, #712, #716, #718, #720, #721, #722, #723, #724, #806, #809, #894) and no new number.

### WP118

- No plan step was skipped or deferred under D25's data rule. Part E2 ran: its commit is whitespace only in lords.xslt, touches no reference, and the validator shows no new ERROR.
- /deep-review skipped as instructed; the independent reviewer runs next. The plan's Maintenance notes list nine probes for it.
- Refs line for the pairing guard left out of commit A1 because the Status block still reads #TBD. docs/features/load-time-stamps.md says the guard has no issue number yet.
- XML merge harness on the public Armory is owed: this run used the installed Armory v2.0.26. Re-run under D25 rule 7 when the public Armory is installed.
- WP103's test_dotnet_commands_flagged.py is absent (the file does not exist; that run reported NO TESTS RAN), so Step D2's check is waived under D25 rule 4. Run it after WP103 merges.
- Plan's own deferrals stand: LOAD-TIME-07 (D14), ButterLib Save System (SAVE-TIME-09, WP114), Yotthani provenance note (WP144), patch-targets.md regeneration (WP137), tools/README.md row for complete_lords_xslt.py (WP142), binding-gate.runsettings (WP122).

### WP119

- The SpecialResources no-record load gap, and the retention fixes in the investigation's Part B table.
- The MEMORY-05 and SpecialResources or race-log GitHub issues the plan assigns to the orchestrator are not created.

### WP120

- Step E /deep-review: skipped as instructed, because this session cannot invoke skills and an independent reviewer runs next. Probe the plan's 'Maintenance notes' review spots.
- docs/features/map-perf-profiler.md rows owed: the plan's nameplate row and two map-view rows. Step 0.3 printed 'map profiler doc absent', so the integrator applies them after WP117 and WP120 have both merged.
- Translations of the four new strings (WP135, paid). The 12 language files carry English rows until then.
- patch-targets.md and gamemodel-bases.md snapshot drift: pre-existing at the base (SNAP0=1), left for WP137.
- Frame-time and VRAM measurements on v1.4.8: no saving is claimed for either; both are in the in-game checks.
- No plan step was deferred under D25's data rule. Parts A and B stage only string rows in ModuleData. The Step 0.5 D20 Armory check is waived by D25 rule 3 for these strings-only commits.
- WP123 Step C2 folds NameplateCullSettingsProviderTests into CampaignHotPathSettingsProvidersTests, as the plan already notes.

### WP123

- No step was deferred by D25's data rule: this WP touches no ModuleData.
- Issue numbers: the new work (Steps A4 and D3) has no GitHub issue, so no Refs line was written; the orchestrator or maintainer creates it (the plan says the orchestrator creates it before implementation lands).
- The plan's optional /deep-review was skipped, as instructed; an independent reviewer runs next.
- Stale row left for WP142 as the plan says: docs/reference/feature-map.md:74 still lists AutoResolveDiagnostics.
- Follow-ups the plan keeps out of scope: the lord-template roster XSLT (plan 134's disposition row for bb68eeca3 item 3, D13); the Mixed Formations hotkey parse cache and strict rule (not porting); PERF-01 and PERF-07 (WP124); demoting per-agent [BattleLoad] lines is not needed, since D3 did not escalate.

### WP124

- /deep-review per part: skipped as instructed, an independent reviewer runs next. Probes for that reviewer are in the plan's Maintenance notes (Remove stays parked, off-thread queries never rebuild, all tree clocks on UtcNow, listener counts change only in Subscribe/UnSubscribe, FindLaidOutFormation lock-free read with locked writes, the cavalry gate still evaluates, StopRunaway stops before logging and cannot throw, the NaN behaviour of the reordered cadence gate).
- Step A4 'Refs #NNN' line: no issue exists, so it was left out and the body is as the plan gives it. Orchestrator owes the issue for FREEZE-THREADING-09.
- D25 data rule: no ModuleData touched, so no step was deferred under it.
- Plan's own out-of-scope follow-ups: the 2 s grid cadence (old plan 003 PERF-01), the per-frame howdah skeleton fetch, the grid holding the last battle's agents until the next mission, plan 015/032/033 lessons (WP142), and the 1.5.x commits after planning (53cf195ac, f2ee9a16f, e65cfda37) for a later sync.

### WP125

- /deep-review (Step 15.2) not run: skipped by assignment, and this session cannot invoke skills. A reviewer runs next; it should probe the plan's Maintenance notes list.
- docs/reference/feature-map.md row for Shadowfax Screens, the b747cf398 and 943ab74c0 lessons, the agent-teams.md checklist line and the two 1.5.x RCAs: left to WP142 and WP143, as the plan says.
- The 63 other rows the snapshot generator produced for patch-targets.md (earlier WPs' patches, including WP117's nine MapPerf rows): not merged here, per Steps 10 and 14 and WP117's deferral to WP137. The file now reads Patches: 249; the generator counts 309.
- No D25 data-rule step applied: this WP touches no ModuleData.
- No GitHub issue needed: the plan cites #1019 and #851 to #854. Maintainer-only files were not needed.

### WP126

- No plan step was skipped. D25 rule 2 allowed Step 2 (CharacterImagePatch.json is config data with no item, roster or mesh reference; the validator showed no new ERROR before the commit).
- Owed on Armory refresh (D25 rule 7): re-run `FullyQualifiedName~CultureRaceConsistencyTests` on the public Armory. The installed Armory reads v2.0.26, so 'every 1.4.x race is registered' is measured on v2.0.26 only. If the public build lacks a race the gate fails; that is the plan's STOP (report the races, do not edit data or the test).
- Step 0.2's Armory-not-v2.0.26 check printed v2.0.26 and was waived under D25 rule 3.
- /deep-review skipped as instructed; an independent reviewer runs next.

## In-game checks owed, per work package

### WP104

- After merging and deploying a normal build, boot to the main menu: no inquiry titled TAOM appears, and the newest taom_debug log has no [PatchApply] line.
- Start or load a campaign: the log has nine `[TableauDiag] PatchCategory '...' applied OK.` lines (Patch1, Patch2, Patch3, Patch4, Patch5, Late_Transpiler, Late_ActionSetOverride, Patch67, Patch72), still no [PatchApply], and no red 'TAOM: patch groups failed to apply' chat line.
- Start the first battle: a `[BattleLoad] seq=1 ... phase=EncounterStart` line appears, and there is no red failure line at mission start.
- New campaign, character creation: the Player Switcher panel appears on the face generator, and the log has no `Player Switcher disabled for this session` line.
- Load a second save in the same process: it loads normally.
- By design, the failure notice itself is not checked in game (it would need a deliberately broken build). The unit tests drive real Harmony 2.4.2 for both failure kinds.

### WP105

- Boot to the main menu: no inquiry saying 'TAOM: feature modules failed', and the TAOM log (Logs\taom_debug_<timestamp>.log) has no [Module] line.
- Wanderer refusal still works (items 1, 2 and 5 of 'How to verify in game' in docs/features/wanderer-allegiance.md). Mordor character in Bree (town_EN2), Aragorn, 'I can use someone like you in my company': Free refusal line, gold unchanged. Gondor character in Isengard (town_isengard), Maztog: Evil refusal line. Mod Options, TAOM, World / Wanderer Allegiance, master toggle off: vanilla hire returns without a restart.
- Save, reload from the main menu, repeat the Aragorn check: still refused (the module adds the behavior on a loaded campaign too).
- One field battle and one town visit: both load, and the log's [BattleLoad] TAOM-behaviors count equals a pre-WP105 build's for the same kind of mission (no module adds a mission behavior yet).
- Custom Battle starts and plays (the BasicGameStarter branch of AddGameStartContent).

### WP106

- 0 (optional, before merging): custom battle, Mod Options, TAOM Crash Report page, QA Dev Triggers, turn on 'Throw On Next Mission Tick'. Expected if the hazard is real: freeze with audio; kill the game. If it does not freeze, report it (V04 recommends /investigate).
- 1 (after merge): same toggle. The crash-report inquiry appears and the battle keeps running after you close it. The log holds '[TickRecovery] armed' at game start and one '[TickRecovery] Mission.OnTick threw TaomDevTriggerException ...' ERROR. Repeat in the same battle: a second ERROR line.
- 2: large field battle with reinforcements and at least one warg rider, 15 minutes. No freeze. Off-thread reports read 'parked for the next mission tick, which is expected' at WARNING. No on-screen '[TAOM] ... ran off the main mission thread' message.
- 3: normal battle, siege, tournament and their exits: no [MissionStall] line and no '[TickRecovery] Mission.OnTick threw' line. TAOM Battle Load Diagnostics MCM page, group Exit Stall Sampler, shows 'Enable Battle Freeze Sampler' ON.
- 4: a career hero with an active ability falls or is knocked out: buffed allies return to normal speed and damage.
- 5: cavalry battle: dead mounts still fade after the configured delay.
- 6: won field battle with field-commission eligible troops: promotion offers still appear.
- Logs: the session's taom_debug_<timestamp>.log in TAOM's Logs folder. Use a build where TAOM and TAOM.Dependencies come from one build.

### WP107

- Deploy TAOM and TAOM.Dependencies from one build, on the merged branch. Pairing: taom_debug_<timestamp>.log has a [BuildStamp] line without MISMATCH.
- Boot: TAOM log shows '[CrashReport] main thread id N recorded at Subscribe()' and '[CrashReport] Native2Managed: attached 16 of 16 Finalizer(s) in X ms' (was 'attached 247 Finalizer(s)'), no 'target not found, skipped' line. Note X.
- First game start: <game>\Modules\TAOM.Dependencies\diag.log shows 'OnGameInitializationFinished: entered (game start: campaign, custom battle or editor)', then '[PatchShield] shield pass: +N new, ... (seen: S, attached: A) in T ms (P ms/attach)' with N about 247 lower than the +354 of 2026-09-02, and one 'not shielding TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp (ExcludedTargetMethods, a hot target), patched by ...' line.
- Capture: Mod Options, TAOM crash-report settings, QA dev-trigger group, set 'Throw On Next Application Tick' once: one bundle and the inquiry, report.txt has Stack Frames. Set it again: TAOM log shows 'occurrence #2, bundle suppressed (logged at 1, 2, 10, 100, ...)' and no second bundle. Do not use 'Throw On Next Mission Tick' here until WP106's recovery is verified.
- Battle: a 500-agent field battle to the end then a clean exit: no new [CrashReport] line, no 'could not tell which shielded method' line in diag.log, and at exit 'SESSION SUMMARY: shielded A of S patched method(s) seen, ... rethrew R with the stack preserved'.
- Combat log (Part C): an elephant or mumak trample that knocks the player's character down shows no 'Crushed through!'; a warg or spider bite on the player's character reads as pierce damage.
- Hints: both capture toggles' hints say they take effect immediately.

### WP108

- Patch90 applied: newest taom_debug_*.log has no '[PreloadGuard] init failed' line and no failure for Patch90_PreloadBodyGuard; console 'taom.print_patches Patch90' lists it as applied.
- Healthy loads: campaign field battle, a siege if available, a tournament and a custom battle each load as before, with no [PreloadGuard] line in the log.
- Guard fires (maintainer only): back up one Armory item file, set a shield or bow carried by a Custom Battle troop to body_name bo_this_body_does_not_exist, restart, start that battle. Expect the first frame to hold about 5 s, the battle to run, and exactly one ERROR '[PreloadGuard] dropped 1 unresolvable collision body name(s) after 5.0s ...'. rgl_log_errors_<pid>.txt may carry 'get_object failed for body'. Restore the file, restart, and confirm a normal custom battle logs no [PreloadGuard] line. The plan's matching MISSING_COLLISION_BODY validator check needs B4.
- Triage note for player reports: a freeze as the loading screen disappears, with BattlePlayable as the last [BattleLoad] line, is this class; ask for taom_debug and rgl_log_errors_<pid>.txt. A WaitingForRender stall with no BattlePlayable (the 2026-11-01 Harad report) is not.
- D20 maintainer step: install the public 2026-09-29 Armory on the laptop so B5 can run.

### WP109

- First start, main menu: `[SkeletonBuffer] guard ON pool 1: site=0x886B4 resume=0x886C1 cave=0x... counter=0x... scanMs=...` and `[SkeletonBuffer] guard ON pool 2: site=0x898F0 resume=0x898FD ...` in taom_debug_*.log.
- Guard toggle: untick 'Skeleton Buffer Guard', restart, expect `[SkeletonBuffer] guard OFF: setting off (MCM Skeleton Buffer Guard)`. ON lines instead mean MCM was not readable at the main-menu phase (the provider falls back to on): report it.
- Game init: `[MissionStartGuard] ON: 6 call sites wrapped in Mission.AfterStart (toggle on)`.
- Trigger, guard on: tick 'Throw On Next Mission AfterStart' (Enable Crash Capture on) and start a battle. It starts; one on-screen message; ERROR `[MissionStartGuard] AfterStart threw in TAOM.Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior (assembly TAOM)`; WARNING `[MissionStartGuard] mission start survived 1 failed call(s); 1 shown on screen`; BattleLoad reaches `BattlePlayable`; no 300 s watchdog bundle.
- Trigger, guard off: untick 'Survive Mission Start Failures', repeat. One `Mission.AfterStart threw and was not survived` ERROR; Modules/TAOM.Dependencies/diag.log gains a SaveShield `swallowed TaomDevTriggerException ... FinishMissionLoading [MISSION-INIT]` line; record whether the second load starts the battle. Tick the setting back on.
- Large battle and siege (corpses unlimited): one `[SkeletonBuffer] mission peak:` line per mission with a `pool 2 peak:` clause; record both percentages. Overflows above 0 mean wrong bones for a frame, never a freeze.
- Custom Battle: one start (wrapped calls outside the campaign).
- Desktop: run the filter FullyQualifiedName~InstalledBinaryTests there; a skip means its native DLL is another build and needs its own rows.

### WP110

- (#764, A1) Play past Full War (MCM Enable Test Mode puts it on day 3), exit to the main menu without closing the game, start a new campaign. Expect 'War of the Ring: phase reset to Peace at campaign start; a load restores the saved phase next', no '[Momentum] ... enrolled' line before 'War of the Ring: Transitioning to FullWar', and no meter until Full War.
- (#772, A3) Test Mode on, new campaign: on day 3 expect 'War of the Ring: <a> declares war on <b> (hostile tier)' lines with map notices and encyclopedia history entries, and no 'had no effect' warning naming a shipped pair. As a kingdom ruler, count the call-to-war popups at the transition and report the number.
- (#772 repair) Load a Full War save made by v2.0.28 or earlier: expect 'War of the Ring: declared N missing Full War wars (#772)' once; save and reload: the line does not appear again.
- (#792, A4) Load a long campaign past Full War: no '[Momentum] Save: the chunk count ... is out of range' warning, and the meter matches its pre-update state.
- (#1015, A5) As a vassal inside another lord's army, take a town: the career quest's SettlementsCaptured advances and momentum logs a player siege event. Enlisted and sitting a siege out: no credit.
- (#1017, B) With an old Configs/ModSettings/Global/TAOM/TAOM.json (for example one holding Phase1TriggerDay 2), launch: MCM shows Phase 1 Start Day 30 and Phase 2 Start Day 44; after saving options the file holds WarPhase1TriggerDay. The bandit, Smart Cavalry, Formation Presets, Battlefield Promotions and AI Party Size options show their defaults.
- (C2, C4) Turn on Enable Victory (End the War) in a Full War campaign: the popup shows 'Victory lead (needs 500)' with the lead in one column; when the war ends, the '[Momentum] War of the Ring decided:' line ends with 'by threshold' or 'by elimination', both roster sizes and the gate.
- (C3, #773) After playing a campaign in one process, load a save made before the momentum feature (before 2026-07-03): the momentum starts fresh (no inherited rosters or ended war).
- (C5) Every load logs '[WarOfTheRing] Session launched' with 'phase days 30/44 from MCM' (or the real source).
- (C6, #774) Watch a raid where the raiders beat a village's militia: Villages Raided rises by one per looted village, not two.

### WP111

- Part A, 1: Custom Battle at the largest size with Ragdolls and Corpses on Unlimited. Expect one `[BattleSettings] ragdollOption=5 corpseOption=5 battleSizeOption=<n> taomOverride=on fadeSeconds=60 corpseCap=25` line once deployment ends in taom_debug_*.log; bodies fade about 60 s after settling; never more than 25 remain.
- Part A, 2: turn Clean Up Battle Corpses off mid-battle. Expect a second `[BattleSettings] ... taomOverride=off` line.
- Part A, 3: a town fight, an arena and a hideout. Expect no `[BattleSettings]` line and bodies stay.
- Part A, 4: first main menu with high options. Expect one inquiry naming the options as the Options screen does. Back up engine_config.txt and BannerlordConfig.txt, click Apply: number_of_ragdolls and NumberOfCorpses change, Options, Performance shows 5 and Low (25), and a `[BattleCorpses] recommended ragdoll and corpse options saved` line appears. Keep mine changes nothing; with Recommend Battle Settings off there is no inquiry after a restart; ragdolls at 1 stay at 1 after Apply.
- Part A, 5: after WP117 lands, watch `[MissionPerf]` agents against active as bodies fade (does a fade free an agent slot?), and run a burst test (cap 250, fade 300, then fade 10, then cleanup off and on) read beside `[MissionStall]`.
- Part B, 6: on a long save with cheat mode, `taom.print_stuck_battles` lists every map event with healthy counts, age and verdict. After one in-game hour, a battle 12 or more hours old with a side at 0 gets one `[StuckBattle] <label>: ...` line and is gone from the next print. The player's own battle and an enlisted player's commander's battle are never named in a `[StuckBattle]` line.
- Part C, 7: any field battle. Expect one `[SmartCavalryAI] mission init` (or `disabled via MCM`) line and one `[MixedFormations] mission init` (or `disabled via MCM`) line per battle.
- Part C, 8: campaign siege assault as a mounted player (Siege Dismount on, mode 3). Expect a `[SiegeDismount] siege detected` line naming `behavior=AutoRemountAfter`, the player on foot, mount and harness in the inventory; after the siege, `[SiegeDismount] mount restored after siege` and both back in their slots.
- Part C, 9: Custom Battle siege assault. After `[SiegeDismount] siege detected`, the log reaches `[BattleLoad] ... phase=MissionAfterStartDone` and then `BattlePlayable` once. FAIL if `MissionAfterStartBegin` and `LoadMissionBegin` repeat (the #699 loop).
- Part C, 10: Custom Battle field battle. The two teams still fight each other (CustomBattleTeamFixBehavior unaffected).

### WP112

- Musician (#824): in town_DG1, town_GT1, town_isengard, town_L1, town_LN1, town_R1 and town_GBC1, talk to the tavern musician several times. Expect no crash and about one lyric in four naming a town. In a Gondor town a lyric names a Gondor town. No [BardTownGuard] warning at game start.
- Hollow parent (#1076), needs an affected save: no crash at the birth, a [StaleCharacterRepair] census: warning at load, and [Check] id=taom.stale-character.parent-repair and parent-birth lines. A healthy game writes none of them.
- Supply (#1064): order a remote supply caravan to a lord in the field. Expect no crash and a route that follows land.
- Main menu (#807): boot to the main menu. The rename shows and there is no [MainMenuStep] warning.
- NRE guards: finish a tournament (winner panel draws). Become ruler (the cinematic plays or aborts cleanly with input released). Start character creation for a horseless culture (no crash; at most a [CharacterCreation] skipped spawning narrative mount warning). Watch an AI siege start (no crash; [SiegeDiag] lines only for real dead ends).
- Patch125 (#1063): a new campaign with the matching TAOM_Map shows no Patch125: warning.

### WP113

- Triage, no game: run python tools/triage_battle_load.py on the two 2026-11-01 Harad logs. E3 should say silent 92s before the log ended and cannot separate a slow cold compile from a main-thread wedge. E4 should use the same wording for the 806 load at 206 s, plus the Same-session rate (heuristic) line with about 35 s. Not run here: only the synthetic test logs were exercised.
- Startup notice (#752, Part B): toggle a shader-less mod such as BetterTime and start. Expect the window over the loading screen within seconds, counting up, no focus taken, closed at the main menu, Below normal in Task Manager while up, both [ShaderNotice] lines, and [ShaderCache] rewrittenThisStart=true with a low sacx=. Start again unchanged: rewrittenThisStart=false. Also check exclusive fullscreen.
- Freeze sampler (Part C): (a) 10 minutes of normal map play gives no [Freeze] line. (b) Alt-tab away for 30 s on the map gives no [Freeze] line; if lines appear, report it. (c) Saving a large campaign gives at most one stack-less line at 40 s and one recovered line. (d) First battle after the BetterTime toggle gives [Freeze] lines with rglCompiles rising, then recovered after about N s; note whether the stack reads <capture failed or <no managed frames. (e) In Visual Studio, freeze only the main thread for 90 s, then continue: four [Freeze] lines with stacks, then recovered.
- Render-wait notice (Part D1): during check (d) the window says Preparing the battle: N shaders compiled while compile lines arrive and closes at BattlePlayable. Check borderless and exclusive fullscreen; the game must not lose focus or minimize. It uses the owned window from 82645b53f while the owner's thread is blocked, so watch for any hang when the window is created.
- Stale notice (Part D2): kill the game during a cold render wait. At the next main menu the inquiry carries the still being prepared sentence (compile lines seen) or the had stopped loading sentence (none for 60 s or more).
- Dump (Part E): enable Write Memory Dump On Freeze and repeat check (e). Expect one taom_freeze_*.dmp of a few MB in Logs and [Freeze] memory dump written; WinDbg ~*k shows the main thread. The dbghelp call has never been executed: no test reaches it.

### WP114

- Startup: `[SaveLoad] ... phase=Runtime framework='...' clr=... serverGC=... latency=...` appears once.
- One fresh-process load and save: `phase=LoadRequested ... inGame=false loadsThisProcess=1`. `phase=LoadCounts objects=X containers=Y` equal the rgl `[GameData.Read] Object Length` / `Container Length`. `phase=SaveBegin ... gc=... privMB=...`. `phase=SaveBehaviorData behaviors=N ...`. `phase=SaveCollected objects=X containers=Y ... collectMs=Z` with X, Y equal to the rgl `[GameData.Write] ObjectData Length` / `ContainerData Length` and Z near `SaveContext::CollectObjects completed in`. `phase=SaveCompleted result=Success loadsThisProcess=1 ...`.
- An in-game reload, then a save: `inGame=true loadsThisProcess=2`; `GameInitializationFinished ... prevCampaign=collected|alive gen2Since=N`; one `phase=SaveCensus reason=afterReload` block. Also note the census `passMs` on a large save: it is on by default and unmeasured on a 2 M object save.
- With cheat mode: `taom.arm_save_census` echoes the armed state; the next save writes `phase=SaveCensus reason=console`.
- Load time: one load of the same save before and after Step B4 (LoadContext phase times in rgl). A small drop is expected, size UNVERIFIED.
- Repro matrix cells 1 to 3 in docs/investigations/save-growth-2026-11.md (cells 4 to 6 as the decision table directs), then `python tools/diff_save_census.py <log>`. Hand the logs and the diff to the orchestrator for the follow-up plan.
- #628: start a new campaign; the `InitialChildGeneration: created N children across M clans (K excluded)` line shows the goblin, Misty Mountain and Blue Craig clans among the excluded (K above a pre-port log). Over a long campaign orc clans stop sitting at twice their cap.
- #793: save in the first half of a day, reload, and check whether a daily handler with a visible effect runs twice for that day.

### WP117

- First game start: `[TickProfiler] probe install: category applied, enabled by hitch probe, targets Mission.OnPreTick,...` with the bookkeeping cost; no `[PatchApply] ... Patch98 ... FAILED`; no `camera bracket not installed`.
- A field battle of a few minutes: `[MissionPerf]` every 5 s; one `[PerfContext]` (note `clr=`: 6.0.x would mean CoreCLR); an `anim-loading sample` median line at the first measured mission; `[TickProfile]` windows. No crash in the first battles with the probe on: a native fault in the clip-loading sample would crash with no probe line first.
- A large siege: `[Hitch]` and `[HitchDetail]` pairs for slow frames; count INFO lines per minute (classifier estimate 2 to 4 per 5 s besides the heartbeat).
- A/B of the probe's cost: three battles each with Enable Hitch Probe off and on, restarting between; compare `[MissionPerf]` `avgMs`; `tools/perf_runs.py` reads the logs.
- Tick profiler on, restart: `attribution install: Mission.SpawnAgent sites 2/2, ManagedScriptHolder.TickComponents sites 5/5`, and `[TickProfile] top=` names behaviours.
- Clip probe: `[AnimMem] armed: TaleWorlds.Native.dll ... loadSite=0x23AADF budgetSite=0x23AB04 counter=0xDA59B0 budget=0xB2BC9C budgetBytes=12582912`, periodic `[AnimMem]` lines, a summary at mission end, no `[AnimMem] disabled` on build 119303.
- Map profiler on, restart, two minutes on the map with fast forward: `[MapProfile]` every 5 s, an install line naming 6 core targets and 2 views, `[MapProfileSummary]` at quit to menu.
- Lost Footing on, a field battle: one start line, census lines while someone is stranded, one summary.
- A frozen battle, if one happens: `[MissionPerf]` lines stop; read WP106's `[MissionStall]` stacks; no `[Hitch]` line for a frame that never ends.
- Tools: `python tools/perf_runs.py <log>` on a new log; `python tools/audit_battle_equipment_memory.py` against the installed packs into a scratch folder (it deletes its previous report there).

### WP118

- Boot to the main menu. taom_debug: [LoadStamps] ready, [LoadXml] ready, [Lifecycle] ready: listener binding ok, [XmlMerge] fast path ready, [ButterLibDistance] switched off ButterLib's Distance Matrix, [buffered HH:mm:ss] [TAOM.Dependencies] lines near the top, [PatchApply] phase=OnSubModuleLoad scope=total and phase=MainMenu. No 'stamps off' or 'fast path off' warning. Session snapshot lists Bannerlord.ButterLib.Implementation.1.4.8 v2.12.0.0 and Bannerlord.MBOptionScreen.v1.4.8 v5.12.3.0.
- Load a pre-port save. rgl: window from 'Initializing saved game end' to 'Main_map/atmosphere.xml' against the pre-port 22.6 to 29.7 s, and the gap between TAOM character file 'opening' lines (0.24 to 0.28 s before). taom_debug: [XmlMerge] type=NPCCharacters ... path=fast, [XmlMerge] summary game=Campaign ... fast_path=on, [LoadXml] lines with merge_ms, [LoadXml] summary, [Lifecycle] dispatch=OnGameLoaded, [MapLoad] heartbeats, LOADING-WINDOW lowered before the map. Any 'stands aside', 'fast path failed' or 'disabled for this session' line is a finding, as is any missing NPC, item or settlement. Modules/TAOM.Dependencies/diag.log names MBObjectManager.LoadXML, CreateMergedXmlFile and the five CampaignEventDispatcher methods as not shielded.
- Return to the main menu and load the same save: xslt_cache_hits above 0 and a shorter merge window than check 2.
- New campaign through character creation: [Lifecycle] dispatch=OnNewGameCreated, map loads normally.
- Custom battle: [XmlMerge] summary game=CustomGame, no [Lifecycle] line, battle plays.
- Mod Options: MCM opens with TAOM's pages and Battle Load Diagnostics shows 'Enable Load-Time Stamps' off. Turn it on, restart, load: per-group [PatchApply], [LoadPhase] and per-handler [Lifecycle] lines appear.
- Crash Report dev setting 'throw on next application tick' writes a bundle.
- Capture a castle: no quarter-second stall at the siege end (with WP117's map profiler, no ~170 ms frame).
- Leave the main menu open five minutes: taom_debug gains no stream of loading-window lines.

### WP119

- None of these can pass until the WP is finished; only 1 to 4 can be run on commit A alone
- Enlistment, second campaign in one process: enlist in campaign A, exit to the main menu, start campaign B without restarting, enlist again. At the first town stop the 'The column halts' offer appears and taom_debug logs `[Enlistment] offered shore leave on arrival at '<settlement id>'`. When the commander leaves, the player leaves too (`[EnlistDiag] EXIT: left the settlement to rejoin the column`), not hours later.
- Enlistment, earlier save: while enlisted, save, play on, then load an earlier save of the same campaign from the in-game load screen. The next town stop offers shore leave and the exit follows the commander.
- Enlistment, once per stop: accept shore leave, walk out with vanilla Leave after the commander leaves, come back to the same town more than a day later. The offer appears again.
- Game end: exit to the main menu from an enlisted campaign. The menu appears normally, with no new exception in taom_debug or rgl_log_*.txt.
- SpecialResources (needs commit B, not landed): play campaign A, exit, start campaign B with another culture. The map bar shows only B's resource at its starting amount, and taom_debug logs `SpecialResources: Seeded starting resource for ... at character-creation finalize`.
- HeroRace (needs C1 and C2, not landed): a new game logs `RacePersistenceService: new campaign, no race data to restore yet.` and no `No saved race data found` warning. Loading a normal save logs `RacePersistenceService: Restored race for N heroes.`
- MEMORY-05 (needs Part B, not landed): run docs/investigations/second-game-retention-2026-11.md once and attach the counts, heapMB values and root paths to the MEMORY-05 issue.

### WP120

- Part A1: with cheat_mode = 1 in engine_config.txt, load a campaign. Expect one '[CheatScreenGuard] ON:' line at game init. A 'not fully installed' warning means a gate is off: report it.
- Part A2: press I on the map. Expect only your own items and no Discard list of thousands; '[MemStation] enter/exit screen=GauntletInventoryScreen' heapMB values differing by well under 100 MB; one '[CheatScreenGuard] cheat mode is on: the inventory opened without the cheat list' line. Record whether the chat notice was visible, on the screen or after closing it.
- Part A3: press P for the normal party screen. A loot screen after a battle and a town trade screen are unchanged.
- Part A4: tick 'Cheat Mode Lists All Items And Troops' and press I. The vanilla list returns, with its memory cost as expected. Untick it.
- Part A5: with cheat_mode = 0, no 'cheat mode is on' line in a whole session.
- Part B1: on a machine with 16 GB or less and Texture Quality High, first launch. Expect one popup at the main menu; '[TextureAdvice] totalPhysMB=... textureQuality=2/2 shownBefore=false: advice shown ...'; the file taom-texture-advice.shown in the Logs folder beside the TAOM log.
- Part B2: next launch, no popup; the line ends 'shownBefore=true: no advice'.
- Part B3: on a machine with more than 17 GB, no popup; the line ends 'no advice'. On a 16 GB machine, lowering Texture Quality and deleting the marker also gives no popup.
- Part C1: game init shows '[NameplateCull] ON: the campaign map skips hidden settlement nameplates (toggle on)'.
- Part C2: at each map zoom level, plates appear and fade as in vanilla; a tracked settlement keeps its marker; walking up to a settlement shows its plate at once; siege and raid events still show on plates.
- Part C3: a hideout spotted by an incident shows its plate in range (the D22 case).
- Part C4: fast-forward five minutes. Expect one '[NameplateCull] 18000 culled map frames: ... skipped (N percent)' line.
- Part C5: run the frame-time A/B in nameplate-cull.md, 'How to A/B it in game'. Before the v2.4.0 release, if it shows no gain or any visual fault, ship the cull off by renaming its key (Maintenance notes).
- Part D1: game init shows '[MapViewRelease] ON: the campaign map's view releases its render targets once per 20 covers (toggle on)'.
- Part D2: exit to the main menu after a minute on the map and open Load. The preview's characters stand.
- Part D3: in Task Manager, Performance, GPU, 'Dedicated GPU memory', open and close the inventory, party and clan screens 20 to 30 times from the map with the setting on, then off. Record both curves: this is the VRAM measurement owed before any saving is claimed.
- Part D4: one return in twenty shows a short loading screen, and '[MapViewRelease] released 1 times, map covered 20 times' appears.
- Part D5: at interval 1, Options, Save, Load and the fief hub (F6) from the map show no loading screen over them; every return to the map renders terrain and does not hang. Set the interval back to 20.
- Part D6: after a release, zoom fully out. Camps, supply routes and the War of the Ring indicator still show.
- Part D7: talk to 20 parties on the map without opening a screen and record whether GPU memory climbs (map conversations are not counted as covers).

### WP123

- Load a save and play 20 minutes on the map: no [AutoResolve] line; Mod Options has no Auto-Resolve Diagnostics group; the save loads with no error; the log is roughly 39 percent smaller than an E4-like session. Log: <game>\bin\Win64_Shipping_Client\Logs\taom_debug_<timestamp>.log (same log for the checks below).
- The session snapshot shows [MissionDiag] Campaign: date=<logged at game init>, MainHero=..., then [MissionDiag] Campaign at game init: now=<date>, ...; no DivideByZeroException.
- Enlisted session: Mod Options shows Enable Enlistment Trace off; no routine [EnlistDiag] TICK or SYNC lines.
- First battle: [MissionDiag] ActionSet census open and ... closed: agentChecks=... newKeys=... lines=...; the ActionSet '...' lines as before; clan-coloured troops keep their armour tint; OOB preset buttons work.
- During a field battle change a Combat Mechanics toggle, Smart Cavalry AI, Dread Aura radius and Mixed Formations in Mod Options: each applies without a restart. Change a Battle Balance slider on the map: auto-resolve follows it.
- Map: Map Figure Scale and the fast-forward multiplier apply at once; an AI lord recruits in a castle; a Free lord's party sheds Evil troops overnight; a Rohan infantry party is slowed; an Isengard town keeps its warg items in stock.
- Refuge defence with a mounted defender and Blow Diagnostics on: the horse takes reduced damage (compare MCM bonus 0).
- Trigger the QA crash (Crash Report, Dev Triggers): the bundle's MCM Settings section lists TAOM's four pages and no other mod's; dropdowns show their selected value.
- Buy Elite Emissary troops: [RosterLoss] DEBUG lines appear only for main-party regulars.

### WP124

- Warg Custom Battle (#659): wargs bite running and standing, bites land and end as before, rage mode starts and finishes.
- Spider battle: pounce and side attacks fire with their usual cooldowns.
- Elephant or mumakil, and a war ram: trample, side attacks and head-butts keep their cooldowns (timers moved to UTC; no interval may change length).
- A battle with no creature: one '[SpatialGrid] Scheduled rebuild skipped: ...' INFO line and, at mission end, one '[BehaviorTree] Mission end: N callbacks skipped with no tree listener ...' line; no WARNING or ERROR from either.
- No runaway: in checks 1 to 4, no 'node transitions in one RunTree call' line, logged or on screen.
- Mixed Formations: in a field battle a mixed infantry and archer player formation gets its default layout within a second, the cycle hotkey rotates layouts, units take their slots, AI formations move as before, the order preview works over a laid-out formation; no 'Patch30 position prefix threw' WARNING and no 'fell back to vanilla' line at mission end.
- Mixed cavalry: a laid-out formation that receives cavalry returns to vanilla positioning (the SmartCavalryAI handshake) on the next position update; in a siege or hideout Mixed Formations stays inert.
- Optional: compare WP117's [MissionPerf] lines on a creature battle and a 600-agent field battle before and after this branch.

### WP125

- Campaign start lines, once each on loading a save: '[Shadowfax] Clan fast open installed: 1 list build site swapped in ClanPartiesVM.RefreshValues', '[ShadowfaxInventory] name sort: swapped 2 of 2 call sites', '[QuestLog] fast open installed; finished quests build when selected (fast open on).' A 'not installed', 'unavailable' or 'left vanilla' line means that screen stays vanilla: report it.
- Clan: headers show counts and refuge rows appear once; disband a party or change a leader and the lists update; rename the clan and lists and headers update; reopen three times, no stale rows. With Log Screen Open Times on: 'list builds: 1 run, 2 skipped'; with Clan Fast Open off: '3 run, 0 skipped'.
- Inventory and trade: on the largest merchant, sort by each column and reopen (the saved sort holds); trade an item (order holds); open loot and a stash (rows sorted); switch hero and press Reset (rows stay sorted); compare with the switch off.
- Party: mixed army with prisoners, every row shows its own portrait (two troops of one type on both sides, a hero, a prisoner); expand an upgrade row and check its portraits; Transfer All and Reset; Talk, return and reopen; repeat once with the switch off.
- Quest log: finished quests list; select one and its stages appear; sort by date started and last updated, switch on and off (same order); a finished quest with an unexamined log shows the updated dot before it is built; turn the switch off while the log is open and select a deferred quest (it still builds); the open line reads 'fast open on;' and 'fast open off;'.
- A/B timing, decides whether each switch stays: per screen, six opens off and six on, on a large save; drop the first of each arm; compare medians of '[ScreenOpen] <screen> open'. Keep a change only if the median drops by at least 16 ms and by more than the off arm's spread; otherwise use the removal checklist in docs/features/shadowfax-screens.md.
- Enlistment board: separate lines on the service wait menu, also on a campaign started this session; updates when the column stops or the day changes; a language switch reaches it within about 2 s.
- Raid from inside (#1019): while serving, have a raid reach the village the column rests in (once of the soldier's faction, once his own clan's), with Enable Enlistment Diagnostics on. Report the '[EnlistDiag] seeding the join from inside ...' line, which menu appeared and what its options did.
- Menu probes (Part C, needs TAOM and TAOM.Dependencies from one build): Enable Map Profiler on, Tick Profiler Top Behaviours 20, restart. Expect '[MapProfiler] menu probes: 3/3 patched' before the install line. 60 s each on the service wait menu at play speed and Fast Forward Multiplier 4, 32, 128, and the same four on a vanilla town wait menu while not enlisted. Send every [MapProfile] line, the [MapProfileSummary] line, the multiplier and the build.

### WP126

- Dwarf portraits: open a settlement with dwarf notables, the party screen with a dwarf hero, and a battle showing dwarf portraits. The face should be framed (eyes, nose, beard), not just hair and eyes at the bottom edge. `taom.print_race_offsets image` should list dwarf as Horizontal 0, Vertical 0.3, Zoom -0.2. Any `[TableauDiag]` 'Spawner frame: race='dwarf'' line in taom_debug should read z=-0.2 v=0.3.
- Dead heroes keep their race: on a save holding a dead runtime-created hero whose race differs from its origin character (most likely a dead child of a non-human player), save with this build, quit to main menu, load, and check the race in the encyclopedia. taom_debug prints `RacePersistenceService: Restored race for N heroes.` on each load. A hero who died before this build keeps the reverted race; that is expected. Where possible also check that a child born after its parent's death takes that parent's race.
- Save time: on one long-campaign save, compare rgl_log timings (`SaveContext::CollectSaveDataForObject::Containers` and the total) for the same save with a build before and after this WP. Expected: no measurable change. A clear increase goes to WP114 with both numbers.

## After the first report: later waves

D26, D27, D28 and D29 released most of the holds, so these merged after the sections above were written. Executor status PARTIAL means the deferred list below names what did not land. `plans/README.md` stays the record of status.

| WP | Executor | Final review | Fix round | Merge | Tests after merge | Conflicts resolved |
|---|---|---|---|---|---|---|
| 127 | DONE | PASS | no | `af3691490` | 12251 | 0 |
| 128 | DONE | PASS | no | `67f9b9aaf` | 12616 | 0 |
| 129 | DONE | PASS | no | `37a478c67` | 12484 | 0 |
| 130 | PARTIAL | PASS | no | `4f99e2743` | 12821 | 2 |
| 131 | DONE | PASS | no | `f938ef85d` | 12324 | 0 |
| 132 | DONE | PASS | no | `1266ab72d` | 12976 | 1 |
| 133 | PARTIAL | PASS | no | `4b07389c7` | 12719 | 5 |
| 134 | PARTIAL | PASS | no | `cf1bc73c3` | 12647 | 1 |
| 135 | PARTIAL | PASS | no | `d2c2fdcf2` | 13047 | 6 |
| 137 | PARTIAL | PASS | no | `b4d2d9477` | 12721 | 1 |
| 141 | PARTIAL | PASS | no | `8b5a21e93` | 12976 | 0 |
| 144 | DONE | PASS | no | `67cf44bd8` | 13047 | 0 |
| 136 | DONE | PASS | no | `1a8153919` | 13099 | 0 |
| 130 F3 | DONE | PASS | no | `48b610da0` | 13047 | 0 |
| 115 | PARTIAL | PASS | no | `ecddc3c72` | 13132 | 0 |
| 121 | DONE | PASS | no | `21800aa91` | 13055 | 0 |
| 140 | PARTIAL | PASS | no | `eb5aadd9e` | 13172 | 0 |
| 139 | DONE | PASS | no | `9c278e95c` | 13355 | 0 |
| 137 D | DONE | PASS | no | `c7c3b3c59` | 13355 | 0 |
| 122 | PARTIAL | PASS | no | `d0b279254` | 13411 | 0 |
| 145 A-D | PARTIAL | PASS | no | `b84c3011a` | 13461 | 0 |

## Integrated review 2 (whole branch, 1ab48c21d to d3a60dfb1)

Seven lenses read the merged branch: merges, patches, threads, state, gates, symptoms, scope. Skeptics tried to refute each finding. Result: 22 survivors, all MINOR after refutation (one MAJOR, the Umbar clan names on old non-English saves, was lowered to MINOR by its skeptic), and 0 lost hunks. One fix branch, `port148/integration-fixes`, took them plus the known minors, passed a cold review with mutation checks, and merged as `6ac230b05` (record `30868fc76`).

Survivors, as the skeptics worded them:

- **MINOR** (`docs/features/mission-tick-guard.md:17 and :104`): The doc still treats WP107 as pending. Line 17 says the `Managed_ApplicationTick` shim's Native2Managed bridge swallows 'everything, until WP107's allowlist'. Line 104 gives an until-WP107 and an after-WP107 behaviour. At tip only the after-WP107 case exists. No runtime effect.
- **MINOR** (`docs/features/map-load-diagnostics.md:9; docs/features/battle-corpses.md:119 and :121; docs/reference/engine/m`): Five notes describe a merged work package as future. No runtime effect. Two corrections to the claim. It is not true that each note was written by an earlier WP: the map-load note came from WP118 itself, which plans/118 line 1116 onward dictates verbatim, and Part A's a0c64e6e2 was already an ancestor of that doc commit 5f5cfdaec. battle-corpses also has a second instance at :119 ('which arrives w
- **MINOR** (`docs/features/gui-sprite-system.md:42, :300 and :352`): The doc lists `CareerSystem/AbilityHUD.xml` as the live battle ability HUD in its file tree and key-file table, and line 300 describes it as displayed via `CareerPerkMissionBehavior` on a `GauntletLayer("CareerAbilityHUD", 50)` with `CareerAbilityHudVM`. The file is gone at tip. One correction: WP140 only made the path rows dangle. The descriptions were already false before WP140, because #382 ret
- **MINOR** (`docs/features/custom-battles.md:56`): The doc says a Custom Battle Sauron fights without TAOM's race combat rules, because they live in the 'campaign-only' TaomCombatMechanicsModel and Custom Battle's damage model is TaomCustomBattleCreatureDamageModel. The behaviour half is false since 76ac53447 (#788, WP130 Part D) and the class name is false on this line. The reviewer's provenance is corrected: the two commits did not land together
- **MINOR** (`C:/Users/mikew/source/repos/taom-port/overnight/Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs`): The #613 stat line is meant for the career hero, but it is written for every hero agent in every campaign mission. Careers are player-only (career-system.md:256, plus companions via CareerSwitchService), so a lord or notable with no career still writes one synchronous INFO line per mission reading 'agent stats for ...: none', plus a 'mount stats' line if mounted. The comment saying the dictionarie
- **MINOR** (`C:/Users/mikew/source/repos/taom-port/overnight/Main/Features/CareerSystem/CareerPassiveService.cs:197-198 wit`): The 'event-scoped' [CareerPerks] lines have no dedupe, and two of the four types fire far more often than the policy states. HeroHealing runs twice per in-game hour for the main party, not on a daily tick. It also runs twice per quarter day for each other party whose owner holds it, a path the finding missed. TroopUpgradeCost runs once per upgrade target per upgradable party-screen row when rows b
- **MINOR** (`Main/_Module/ModuleData/Languages/DE/std_taom_clan_name_strings_deu-DE.xml:112 (same row and line in all 12 la`): Confirmed by reading only, severity lowered from MAJOR to MINOR. In a campaign saved before WP135 and played in any non-English language, clan_umbar_2 to clan_umbar_6 all display clan_umbar_1's name. Today that is 'House of Casîmirionî' (English row); after the owed translation run it becomes that language's Casîmirionî name, for example 'Haus Casîmirionî' from the DE cache. At base each clan show
- **MINOR** (`TAOM.Tests/Features/ShadowfaxScreens/Party/PartyFastOpenPatchShapeTests.cs:41; TAOM.Tests/Features/ShadowfaxSc`): The documented `managed-tests` gate (.ai/verification.md, -c Release) is red at the tip. Two WP125 tests patch stand-in methods with no NoInlining attribute, so the Release JIT inlines them and the Harmony patch never fires. This is a test defect, not a product defect. No integrator record ran -c Release, because the programme's baseline command is Debug. QuestLogPatchTests.cs:308 already puts NoI
- **MINOR** (`plans/README.md:336; docs/reference/taleworlds-api-snapshot/patch-targets.md:35; docs/reference/taleworlds-api`): Narrowed. The `api-snapshot` gate exits 1 at the tip and no record says so; the last recorded result is rc 0 at WP137 Part D (README:334). The drift itself is authorised: D29 rule 2 leaves every Part G step out, and plan 145 Step G6 (:541) names both rows to remove. The README:336 deferral covers all of Parts E, F and G and only gives gamemodel-bases as an example ('including'), so the patch-targe
- **MINOR** (`plans/README.md:297-316; plans/README.md:348; plans/_audit/2026-11-04-port148/OVERNIGHT-REPORT.md:40`): Corrected: the warning series mixes two counting modes, and the cause is now identified. The gap is not a clean export against the worktree. It is whether a build emitted MSB3270 processor-architecture warnings from TAOM.csproj and TAOM.Dependencies.csproj, which adds about 112 to 114 to the count. Both 2251 and 2365 are real results for the base commit, so OVERNIGHT-REPORT.md:40 was right about i
- **MINOR** (`plans/README.md:82; plans/_audit/2026-11-04-port148/PROGRESS.md:53-54`): Programme state text is stale against the rows and git. README:82 says WPs 102, 103, 115, 116, 121, 122 and 127 onward are held, though rows from 115 and 127 onward are merged or PARTIAL under D26, D27 and D29. PROGRESS.md, the file README:84 points to for details, stops at 'Nineteen WPs merged' and 'D25's holds still apply'. No later commit or note corrects either. Documentation only; the rows ar
- **MINOR** (`CLAUDE.md:159`): Narrowed to one count. 'all 83 rows' for doc-lookup is now 85, because 8af702734 and 3205dd766 each added a row and neither updated CLAUDE.md. No plan carries this count, so nothing will correct it, and WP142 Step E2 will add more rows.
- **MINOR** (`tools/data_gates.py:117-118; tools/data_gates.py:192-200; tools/audit_action_set_parity.py:29-36,47`): `data_gates` reports a gate that never ran as FAIL, against its own docstring rule that no install is NOT RUN (exit 1 'is a finding'). audit_action_set_parity hardcodes the desktop's E: paths, the gate passes no --native or --live, and an unreadable file exits 1 through sys.exit(string). Attribution corrected: data_gates.py comes from WP136 Step B7 (5499889a3), not WP141, and the audit tool itself
- **MINOR** (`Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs:44; Main/Features/BattleLoadDiagnostics/T`): The Enable Exit Stall Sampler hint says that turning it off keeps the other diagnostics 'without any thread suspension'. That is false at tip d3a60dfb1. The Battle Freeze Sampler and the Main Thread Freeze Sampler default on, answer to their own switches and both reach Thread.Suspend through ThreadStackCapture.Capture. A player who turns the exit sampler off to remove the suspension risk still has
- **MINOR** (`docs/features/armory-ref-audit.md:74-82, :100, :116-118, :133-135; docs/features/preload-body-guard.md:33, :47`): The docs present WP108 B4's validator gates as live on this line. They name MISSING_COLLISION_BODY, COLLISION_BODY_BORROWED, missing_collision_body_issues, borrowed_body_issues, _loaded_tpacs, MissingCollisionBodyTests, tools/tests/test_collision_body_borrowed and the hook's --code lines. B4 is held (D27 rule 8), so a reader believes a hang-class gate runs when it does not. Plans/README.md:300 rec
- **MINOR** (`tools/perf_runs.py:99-100; docs/features/mission-perf-heartbeat.md:1121-1122`): Both say the Patch89 map-load trace first shipped in v2.0.29 'and the 1.4.5 branch has none'. WP118 ported the trace to this line, so a reader of a v1.4.x log is told to expect no game boundary lines that a build of this branch does write. WP117 landed the sentence before WP118 merged. e1ae44d4f removed the other 'until WP118' notes and left these two.
- **MINOR** (`docs/features/mission-tick-guard.md:87`): The doc says Patch35_Mission_OnTick shares the Mission.OnTick target. WP123 deleted that patch, so the guard's doc describes a neighbour that no longer exists. This is a cross-plan interaction: WP106 was planned while the patch still existed.
- **MINOR** (`commit 0fb873f67 (Ported-from: e45f6df2c2c8cbb52099d6d2ffb7defa136f7766)`): The commit's only Ported-from trailer names the planning pin, a Fords of War commit that touches neither changed file. The rows were added by f1675c058, so the trailer breaks the one-trailer-per-included-commit rule (plans/README.md:113) and points provenance at an excluded feature. The correct trailer is f1675c058. It is unpushed, so it can still be reworded before the maintainer takes the branch
- **MINOR** (`tools/data_gates.py:93`): The moduledata gate's note tells the reader 'settlement pins live in tools/settlement_assignments.json'. That file exists only on 1.5.x. The note came over with the table and points at nothing here.
- **MINOR** (`tools/audit_mbproj_registration.py:113`): A comment cites docs/ai-includes/orientation.md, 'Three modules'. That doc exists only on 1.5.x, and several plans record it as absent here. The local equivalent is the 'Three-module data surface' row in CLAUDE.md Traps.
- **MINOR** (`TAOM.Tests/Features/MapPerf/MapFrameProfilerBindingTests.cs:301; TAOM.Tests/Features/MissionPerf/HitchProbeBin`): Three test comments name CreatureBanditsWiringTests.TargetOf as the shape they copy. That class is on 1.5.x only and this line has no CreatureBandits feature. The first comment also names MissionTickProfilerBindingTests.TargetOf, which does exist, so only its parenthetical is dead.
- **MINOR** (`docs/features/composition-root.md:161`): The kernel-anchor example is `new AgentColorStoreCleanupBehavior(colorStore)`, a line WP123 deleted. WP123 moved the test's anchor as this same doc instructs, but left the doc's example. The kernel tests now anchor elsewhere, so the example names a line that is not in the tree and is not an anchor.

Fixer refuted: mount-parity gate gives no real verdict because of an E: path default: does not hold. tools/audit_mount_parity.py already uses _gamedir. It exits 0 on the laptop and prints 'F4 target-resolution and quad_movement checks SKIPPED', because the installed Armory v2.0.26 has no Assets folder, so data_gates' NOT RUN is honest. No change. A prefab-budget FAIL is real against the cap: does not hold. Measured total is 130,468 of 131,072, exit 1 is only the WARN band above 120,000. The row was misleading and is fixed (see applied).

Owed after the fix branch:

- 0fb873f67 Ported-from trailer: reword e45f6df2c2c8cbb52099d6d2ffb7defa136f7766 to f1675c058's full sha. Not done: it rewrites every later commit and invalidates every 'overnight, <sha>' status in plans/README.md. Maintainer's choice before taking the branch; recorded in the README ledger line.
- Paid translation pass for taom_bc_apply_failed (English placeholder rows) and the renamed aom_clan_umbar_casimirioni_name row (language files still hold English; caches keep the translations under the new key), with the other owed passes.
- VanillaTuning: maintainer re-reads HoN/VanillaTuning at or after 1b9ce3ba and decides whether row 70 of provenance-register.md (shader-compile-notice, uncleared) can be cleared.
- Skill and hook edits the new qualifiers point at: plan 137 and plan 139 .claude hunks, WP108 Step B4, CLAUDE.md:187 and :225 with WP143. Reword or remove the 'once ... land' qualifiers then.
- CHANGELOG.md entry for this branch (never staged, per the rules).
- In game, nothing is run: an old German save showing six Umbar names, a real hollow hero across save and load, a real harmony.Patch failure. /deep-review was not run (cannot invoke skills); recommend /deep-review on efe9c6bfd, e84268475 and 95acf768c before merge.
- Known limits left as they are: a restored human race on an unregistered hollow character still takes the twin's race (nothing tells it from the unsaved default); a manual patch failure is logged but not added to the patch failure report; the Exit Stall Sampler hint stays an unregistered MCM literal.
- Not acted on, noted in delta-974e60bb2.md: DreadMissionGate.cs:38 and MountDespawnMissionGate.cs:33 read only GameNetwork.IsSessionActive (possible co-op gap, unverified, out of scope under D7); the 1.5.x Spacing Repair in Marshal's Word is a possible future work package.
- What switches MSB3270 warnings on is not established.

## Verification at the tip (`30868fc76`), run by the session that wrote this

| Check | Result |
|---|---|
| `dotnet build TAOM.sln --no-incremental` | exit 0, 0 errors, 2505 warnings |
| `dotnet test TAOM.Tests` Debug | Failed 0, Passed 13466, Skipped 8, Total 13474 |
| `dotnet test TAOM.Tests -c Release` | Failed 0, Passed 13466, Skipped 8, Total 13474 |
| `tools/snapshot_api_surface.ps1 -Check` | both files reproduce, exit 0 |
| `python -m unittest discover -s tools/tests -t .` | Ran 3130, failures=3, errors=1, skipped=25, exit 1. The four are `test_no_generator_writes_a_retired_id`, `test_missing_id_becomes_a_warning_under_the_code`, `test_every_culture_on_disk_carries_the_step` and the `test_audit_map_scene_memory` load error, recorded before this programme's later waves (README baseline table) |
| `bash tools/test_hooks.sh` | 207 passed, 0 failed |
| `python tools/lint_docs.py --summary` | total_findings 1 (`claude_budget`, owed with WP143) |
| `python tools/validate_moduledata.py` | exit 1, void as an item verdict until the public Armory is installed (D25) |

Trunk `bannerlord-1.4.5` and `origin/bannerlord-1.4.5` are both `1ab48c21d`; this run did not move them. Nothing here was run in the game.

## Your symptoms, traced through live code (symptoms lens, read at d3a60dfb1)

- SYMPTOM infinite loading, addressed: (1) Patch90 PreloadHelper.WaitForMeshesToBeLoaded prefix, applied in OnSubModuleLoad (SubModule.cs:449-451), service registered (IoC.cs:169), no setting; the v1.4.8 loop at Native__TaleWorlds.MountAndBlade.View.cs:3355-3372 reads the field it drains. (2) Patch103 mission-start guard, GameInit, six swaps in Mission.AfterStart, SurviveMissionStartFailures default true and fail-open. (3) Loading-window closer registered in a finally (SubModule.cs:2059-2068). (4) Cold shader cache: ShaderCompileNotice at OnSubModuleLoad, RenderWaitNotice from MainThreadFreezeWatchdog started at the main menu; the rgl_log path and compile_shader lines are present on this laptop. (5) Patch99 XML merge at ProcessLoad, gated on TAOM.Dependencies excluding it from PatchShield; ButterLib Distance Matrix off at MainMenu, the type is in the 1.4.8 implementation DLL. Remains: WP108 B4 desk gates (deferred); the _uniqueMetaMeshes branch of the same wait is not guarded and its behaviour on a bad mesh is unverified.
- SYMPTOM combat freeze with sound, addressed: (1) Skeleton buffer guard and watch, MainMenu phase, both settings default true, no patch category. I scanned the installed TaleWorlds.Native.dll: watch, pool 2 load, pool 1 guard and pool 2 guard patterns each match exactly once. (2) Patch91b tick recovery, armed after Patch91 at SubModule.cs:1933-1934; it also covers the inline fast-forward TickAgentsAndTeamsImp route, because that route never sets the launch mark. (3) Off-thread parking through DeferredCallbackQueue, with drains at BehaviorTreeMissionLogic.cs:174, MountDespawnMissionBehavior.cs:107 and :121, CareerPerkMissionBehavior.cs:65, WargMissionBehavior.cs:81; FieldCommission RegisterKill takes a lock. (4) Corpse caps, below. (5) Instruments: Patch91 [MissionStall], [Freeze], Patch98 hitch probe default on. Remains: a throw on the job thread from TickAgentsAndTeamsImp now reaches native with no TAOM shim (Native2ManagedTargets, WP107), and what native does then is UNVERIFIED, the same state PatchShieldPolicy.cs:138-153 records. Freeze cause stays unproven per D21. Not a provable defect at this tip.
- SYMPTOM long saves: instruments only, plus one growth brake. Patch61 SaveGraph, LoadCounts and BehaviorSize categories apply in OnSubModuleLoad with no setting; the census runs on the first save after a reload. #628 orc overbreeding fix (863cfbd98) survives WP145's move into RaceAgeService (RaceAgeService.cs:104 keeps the cap rule; diff of ed0929d69 read). Remains: docs/investigations/save-growth-2026-11.md states 'No fix exists yet'; the 71,500-containers-per-reload carrier is unnamed and the repro matrix is owed in game.
- SYMPTOM 15.7 GB memory, addressed: CheatScreenGuard at GameInit, setting default false so lists are hidden; swaps the single CheatMode read in each opener (dump InventoryScreenHelper.cs:177, PartyScreenHelper.cs:187). TextureAdvice at MainMenu, option 2 is High per global_strings.xml:883, marker file only, advice only. MapViewRelease at GameInit via ScreenLayer.OnLayerActiveStateChanged, default on, every 20th cover. NameplateCull Patch104 default on, frame cost rather than memory. Cross-campaign resets from WP119 on load, new game and game end. Remains: WP121 B to E Kit work deferred, and 9e1b03790's sheet drop reaches players only with the next Kit publish; docs/investigations/second-game-retention-2026-11.md names candidates such as _lastSessionStarter on three container singletons (still present at EnlistmentBehavior.cs:29, EnlistmentContentBehavior.cs:28) and CommanderLordAdapter._lastSeenMapEvent:76, with no fix until a heap snapshot names the holder. No plan promises a private-memory number (D23).
- SYMPTOM Harad first-battle freeze, addressed: diagnostics and notices, not a cure. MissionState_OnTick_RenderWait_Patch in Patch43 publishes the count; RenderWaitNoticeDecider claims compiling only while rgl compile lines arrive; BattleLoadStallWatchdog defers while the queue moves; pre-compile menu option stays active per D4 (EnableShaderPrecompilation default true, SubModule.cs:769-793). Remains: V02 says E3 is undecidable from logs and E4's second stall is unexplained; D11 and D12 keep ShaderCacheKeeper and bow shaders out, so every module-list change pays a rebuild. The notice appears only when the main thread is silent for 5 s and needs the Battle Load master toggle (default on) and the Freeze Sampler toggle (default on).
- SYMPTOM ragdoll and corpse load, addressed: BattleCorpsesModule adds BattleCorpseMissionBehavior to every mission; it gates on MountDespawnMissionGate (Battle mode, field, siege or sally-out) and applies fade 60 s and cap 25 from OnMissionTick once a second. Cleanup default true on a new property, so existing installs get it. Cap is the lower of TAOM's and the player's option; the option table matches global_strings.xml:1103-1118. Dead mount despawn stays default on. Advisor inquiry at MainMenu lowers Ragdolls to 5 and Corpses to Low only on Apply, and repeats each launch until accepted or turned off in MCM. Remains: ragdoll count has no per-battle override, so it depends on the player's click; whether removing a corpse frees an agent slot is an owed in-game check in battle-corpses.md.
- SYMPTOM wrong XML data and mesh ids, addressed: runtime Patch90 logs each unresolvable body name at ERROR and names tools/audit_armory_refs.py, which exists; desk tools audit_armory_refs.py and data_gates.py (--help read, the table not run); data fixes that touch no item, roster, equipment, troop or mesh reference (Dunland default_group, seven clan banners, policies, Lindon, backstories per commit subjects). Strings used by the symptom features resolve in taom_module_strings.xml by my key scan, except FreezeDumpWriter.cs's taom_freeze_ prefix, which is a dump file name rather than a string key. Remains: all item, roster, equipment, troop and mesh verdicts are void on the v2.0.26 Armory and owed per D25; WP108 B4 and WP136's dependent gates are deferred.

Not verified: any native behaviour beyond what the docs cite from RVA reads, and any in-game outcome. No symptom can be called cured from this tree; the ones with a mechanism fix are the mission-start reload loop, the missing-body wait, the handshake and skeleton pool overflow, each still owing its in-game check.

## Deferred, later waves

### WP127

- Part M (maintainer, docs/adrs is protected): M1 ADR-002, ADR-007, ADR-008 and M2 ADR-003 and README. Commands are in the plan's Part M; specs m1 and m2 were not run. The executor's messages are at C:/Users/mikew/source/repos/taom-port/wp127-scripts/msg-m1.txt and msg-m2.txt, and wp127_edit.py is beside them. The branch must not merge until both commits are on it, because the rules now cite ADR sections only Part M adds.
- CLAUDE.md one-liner edit, dropped by the plan's C1 fallback; carry to WP143. Change line 225 to: **Mod**: `[HarmonyPatch/GameModel/CampaignBehavior]` -> `Service` -> `IAdapter` (sealed types); `IOnXxx` hook optional (4 bytes longer).
- /deep-review skipped as instructed; an independent reviewer runs next.
- Lessons and records not carried, per the plan: lessons to WP142; deep-review lens wording to WP138.

### WP128

- /deep-review: skipped as instructed; the independent reviewer runs next. Plan's review focus: guard order in ServiceBattleService.TryJoin; MirroredWars entries added at join are exactly what discharge unwinds; the widened BreakStaleBattleLatch predicate and the R1b-inert assumption; DutyText fallbacks; C3 save compatibility. Plan also recommends /verify-bindings after Part A (new Harmony target).
- Translations: paid pass not run (D26). Three keys carry English placeholder rows in all 12 language files until /localize or WP135: taom_enlist_menu_discharged_leave, taom_enlist_wait_discharged, taom_enlist_act_raid.
- Issue references: the plan uses only ported 1.5.x numbers (#827, #828, #831, #954, #832, #960, #834, #835, #1021, #842, #843, #844, #845). No Refs line was added and no issue was created.
- WP142 docs sync, per the plan: tools/README.md still lists generate_enlistment_duty_strings.py; doc-lookup.md and the lessons entries of these commits were not synced; WP142 must not copy the 1.5.x feature-map wording that claims the hourly war follow.
- P128-1 leaves out the hourly war follow and the crown-war release; a later port needs what the plan's Maintenance notes list.
- The docs ModuleData data verdicts stay provisional on the v2.0.26 Armory (D25). Validator summary unchanged at 3768 errors, 120 warnings; no item, roster, equipment, troop or mesh data was staged.

### WP129

- /deep-review after each Part: skipped per the task, an independent reviewer runs next.
- /verify-bindings after Part A (new InitializeMissionEquipment override, new IL pins): recommended by the plan, not run because it is a skill.
- Translation of taom_career_repair_refund (B1) and the corrected taom_ws_t3_a_p3_desc (B3): strings stay English in the 12 language files and the stale cache entries are removed; WP135 runs the paid pass (D26 rule 4).
- Lessons deferred to WP142: 7ede923be (adapters, testing-qa), 35007dbb8 (data-content-cultures), c96a02a4b (state-lifecycle-save), 86ef5c7af (campaign-mechanics, gamemodels-services, testing-qa), 4f5b412d7 (campaign-mechanics), 0761c01b7 (adapters), 180696430 (state-lifecycle-save).
- REVIEW-LOG row, Codex prompt files and docs/INDEX.md and feature-map syncs: not taken, per the plan's Out of scope (WP142 to WP144).
- No plan step was skipped under D25 or D26. The ModuleData changes are strings, one description text, two XML comments and one config list, with no item, roster, equipment, troop or mesh reference.

### WP130

- Step F3 (four Dunland default_group retags, D13, commit 'fix(dunland): v2.0.28 - Hunter and Falcon troops fight as archers'): deferred under D25 rule 2 and D26 rule 3. Nothing in troops_dunland.xml changed on this branch. The plan's script, verify steps and message are ready to run once the public Armory baseline is in, or sooner if the maintainer rules that a default_group tag is not a troop reference. It adds no tests, so the test total stays the same.
- Step G /deep-review: skipped per the task; an independent reviewer runs next. Plan probes: the twin's call order against the campaign model; PassesRaceGate folding the toggle; the E3 kernel anchor move; ShouldAttach and detach-on-disable; no MissionView constructed in Main; the dread doc's v1.4.8 citations.
- Follow-ups the plan routes elsewhere: refresh docs/reference/taleworlds-api-snapshot for the two new models (/verify-bindings, maintainer); regenerate docs/reference/ranged-troops.html at the next ranged-ladder report run; the Hebog-luth horse gap (content, maintainer); lessons from d96f349f1, 40385aa5b, efa32184b, 0103a8271, f4f12a737, 726975a52, 30abf550b, 3f75217dc, 1641fb985 (WP142); gamemodels.md, gui-ui.md and csharp-patterns.md reconciliation (WP143).
- Not ported, by the plan's disposition table: per-culture charge damage, the three extra charge sliders, the TAOM.Client split.

### WP131

- Nothing in the plan's steps was deferred by D25 or D26. B5 stages only strings (taom_enlist_not_by_messenger, base plus 12 languages), which the D25 data rule allows.
- Translating taom_enlist_not_by_messenger: paid, WP135. The 12 language rows stay English.
- Stale after this WP, left for WP143 and WP144 by the plan: .ai/review-reference.md:575 still lists the two deleted Patch42 targets; CLAUDE.md patch-category notes and docs/reference/feature-map.md do not mention Patch114 or Patch117; backlinks footers not regenerated.
- Lessons hunks dropped from every pick (WP142). .claude/rules/moduledata-validation.md gate line goes to WP140.
- The 1.4.x EnableMessengers row still claims the behavior is not registered when off; a messenger docs pass owns it (the plan's own deferral).
- #920 co-op stand-down not ported (D7), so under a co-op session castles still recruit hourly.

### WP132

- Translation of the ten new keys (WP135; D26 rule 4, no paid call). They are registered with English placeholder rows in all 12 language files: taom_enlist_qm_refused_host, taom_enlist_qm_refused_load, taom_enlist_qm_refused_stores, taom_enlist_sitout_wounded_tip, taom_enlist_sitout_odds_tip, taom_enlist_sitout_refused_tip, taom_enlist_sitout_siege_tip, taom_enlist_sat_out_line, taom_enlist_sat_out_wounded_line, taom_enlist_sat_out_forced_line.
- /deep-review skipped as instructed; the independent reviewer runs next. Plan's review focus: the payout's sat-out condition and ledger consumption on every path; that the menu postfix only narrows and fails open; the raid-join deferral (_inMenuFrame, the menu id check, the reset in ResetSessionCaches and Pump); RunDailyTick keeping nowDays; R1b's removal resting on R1.
- Docs sync left to WP142 to WP144: the lessons entries of 1d9cc42f1, 069962aa4, 5d70714c4 and 3b848c411, and b0625f0d1's tools/README.md line.
- P132-8 follow-up: the board line for a battle the oath keeps the soldier out of (OathSitOutRule, a SittingOut activity, taom_enlist_act_sitout in 13 files, ServiceStatusService taking IEncounterAdapter and IServiceDiplomacyService).
- No plan step was skipped under D25 or D26. A0.5 (installed Armory must not be v2.0.26) printed v2.0.26 and was waived by D25 rule 3, because A3 and B1 stage only strings and enlistment_config.json under ModuleData (D25 rule 2). The data half stays provisional until the public Armory is installed.

### WP133

- Step B2 (210943076, #932, Lindon and Blue Craig banner rows): not applied, deferred under D25 rule 2 and rule 3. Run it when the public Armory is installed. Its test-count effect is 0 (the plan's +72 lists no row for it, and the total reached T0 + 72 without it). Its data compare was not run.
- Translation of the new strings (D26 rule 4, WP135): taom_tavern_drink_default, taom_tavern_food_default, taom_str_liege_title.<16 cultures>, taom_str_liege_title_female.<16 cultures>, taom_str_become_king_empire stay English in the 12 language files.
- /deep-review skipped per the task (independent reviewer runs next); /verify-bindings recommended by the plan after Parts A and B, not run (a skill).
- docs/reference/taleworlds-api-snapshot/patch-targets.md rows for Patch111, RefugeClanRowRefreshPatch and the Patch23 retarget: WP137.
- Dropped lessons, feature-map and INDEX rows: WP142. Reconciling the .claude/rules edits (gui-ui.md in C2, csharp-patterns.md in D2): WP143.
- Plan-level deferrals kept as written: Fiefs map-bar button, troll-gear Armory policy and validator, CustomAttacksUtils comment corrections, the 1.5.x RCAs the plan skips.
- GitHub issue for the review 130 config-list fixes (B1) and the culture-market NotMerchandise skip (C7): none exists, orchestrator or maintainer creates them. The plan's messages carry no Refs line, so none was left out.

### WP134

- Step A2, troop half (D25 rule 2): the 22 lindon_ rows in TroopWeights/troop_weights.xml, EveryLindonTroop_WeighsTheSameAsItsRivendellTwin and its summary edit in TroopWeightLevelBandTests.cs, and every docs/features/troop-weight-system.md edit. My reading: a TroopWeight row is a troop reference, even though the ids live in this repo. Only the survival-bonus half landed, so the plan's A2 subject and body were rewritten to match. The later commit needs its own Ported-from: 0e75ec50a34e33fd7f4a6b4c61a3cd15e1330c3d.
- Step C3 whole (D25 rule 2): lord_template_rosters.xslt, its SubModule.xml node, the 70 Dale rows in taom_lord_template_equipment.xml, LordTemplateRosterTests and the equipment-rosters.md paragraph. It touches rosters and items.
- Step C4 whole (D25 rule 2): lord_1_48_1/2/3 in lords.xslt and characters/lords.xml, the custom_battle_lords.xml stubs, LesserNazgulDefinitionTests, and the nazgul-family, custom-battles and uncapturable-heroes docs plus the UncapturableRegistryTests comment. It changes equipment kits and troop definitions, so lords.xslt is untouched.
- On Armory refresh, re-run those three deferred pieces and re-check all data verdicts (D25 rules 3 and 7). validate_moduledata.py results here are measured on v2.0.26 and are provisional.
- Translation pass (D26 rule 4, no paid calls): Part C's 320 wanderer rows sit in each of the 12 language files in English, owed to WP135.
- /deep-review skipped as instructed; an independent reviewer runs next. Maintainer-only files needed no change.

### WP135

- C1 step 3, free fill: `for L in BR CNs CNt DE FR IT JP KO PL RU SP TR; do python tools/translate_with_claude.py --lang $L --module TAOM --apply --max-entries 0 || break; done`. Not run under D26 rule 4. The rows stay English, so names and wave-4 rows do not show translated yet. The commit landed caches only, with the subject changed from the plan's 'fill wave-4 and name rows from cache' to say so.
- C2 as written: the translator's --dry-run was not run. I computed the same counts through its read-only functions, listed under handoff.
- C3, paid translation (maintainer only, M1), about 94 rows per language.
- C4, validate and commit the paid rows, including `python tools/loc_length_report.py --since e3a34b651` (commit message msg-135-15).
- Part D, Italian name grammar tags: optional and the maintainer's decision (M4). Not run.
- D25 rule 3: B3's check of the 35 UNREAD ids against the live modules ran on Armory v2.0.26 and TAOM_Map v2.0.27 (480 files scanned, 0 ids found). Re-run it when the public Armory is installed.
- A0's Armory check printed v2.0.26, which the plan treats as a STOP. It is waived under D25 rule 3 because every staged ModuleData change is strings: name keys in clans.xml, one name text in lords.xslt, text rows, manifests and generated name masters. The validator is unchanged at BROKEN_ITEM_REF 3768 (provisional).

### WP137

- Part D, all of it, stopped by the STOP above: D1 (-Check fix, regenerated snapshot, README counts, impact-doc line), D2 (ManualPatchApplicator header; item 2 would be skipped anyway because the PathReuseCache count is 0), D3 (.ai/verification.md api-snapshot row, /verify Step 2d). tools/snapshot_api_surface.ps1 -Check still prints DRIFT, rc=1, on this branch. The snapshot regeneration WP117 and WP118 left for this WP is still owed.
- Deferred under D25 rule 4 (.claude/ work; Step 0's WP103 marker check printed WP103-MISSING and is not waived for .claude/): B4 entire (native-crash-triage hang procedure Phase 2b, engine-bump items 2 and 4).
- Same rule, .claude/ hunks: A1 (.claude/skills/taom-src/SKILL.md, engine-bump item 3), B3 (.claude/agents/{taleworlds-researcher,debugger,error-detective}.md, .claude/skills/{research,investigate,new-creature-mount,deep-review}/SKILL.md), C1 (engine-bump item 5 and the Phase 3 body-diff paragraph), D1 (engine-bump Phase 4 item 1), D3 (/verify Step 2d).
- Same rule, docs WP102 or WP103 name as their own targets: docs/ai-includes/codex-operating-guide.md (A1), docs/ai-includes/agent-operating-manual.md (A1, B3), docs/reference/mcp-servers.md (A1, B3), docs/ai-includes/external-repo-adoption.md (E1 packageSourceCredentials line; grep count on the branch is 0).
- Translations: none needed. In-game checks are owed at the end (D26).
- Ghidra PINS row for v1.4.8 (maintainer step 5); archive session-start wiring (plan's deferred-by-decision list).

### WP141

- Step 0 preconditions for WP102 (changelog_from_commits.py absent, check-changelog-changed.sh present) and WP103 (no Bash|PowerShell matcher) failed. Both are waived by D25 rule 4 for work outside .claude/, tools/test_hooks.sh and the docs those plans rewrite. The WP137 precondition held.
- D1 step 3: .claude/hooks/check-graphify-usage.sh (git show e45f6df2c:.claude/hooks/check-graphify-usage.sh) and its smoke test. Not landed because it is .claude/ work.
- D1 step 4: the tools/test_hooks.sh section '7-graphify', the prefilter rows and the starved-environment payload change. Not landed, since WP103 and WP116 rewrite that file. The plan's verify of 10 check-graphify-usage rows is therefore also deferred.
- D2: registering the gate in .claude/settings.json (maintainer-only, needs WP103's Bash|PowerShell group and the hook file). Exact change, after the last hook of the PreToolUse Bash|PowerShell group holding validate-push.sh: add {"type": "command", "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/check-graphify-usage.sh", "timeout": 10}. Script written, not run: C:/Users/mikew/source/repos/taom-port/141-scratch/settings_graphify.py. After it runs, land the settings.json commit 'chore(hooks): v2.0.28 - register the graphify gate for both shell tools' (Ported-from b8833474e).
- After D1 steps 3 and 4 land, revert the 'not on this line yet' wording that replaced the plan's text in docs/features/graphify-code-graph.md (Overview paragraph and the three Key Files rows, which are now plain text instead of links) and docs/reference/provenance-register.md.
- A5's check of the 24 fallback kit ids against the installed modules: it printed no generate_enlistment_rosters.py line, but it was measured on the v2.0.26 Armory, so the verdict is void under D25 rule 3. Re-run the plan's findings command when the public Armory is installed.
- GitHub issue for the plan's new work (renamer test file, RDC gate test, civilian-set filter in its 1.4.x form, enlistment fallback-table spec): orchestrator or maintainer. No commit carries a Refs line for it.
- Plan's optional /deep-review was skipped as instructed. No paid call and no translate_with_claude.py run was made.

### WP144

- A5 Armory measurements were taken on the installed v2.0.26 Armory, not the public build (D25 rule 3). The hair and beard distribution table and the dwarf and Saruman race sentence are labelled v2.0.26 in the doc; a re-measure on the public Armory is owed.
- B4 sub-edit 9 (skeleton fact 4 and the troubleshooting row) skipped: `VERBATIM` is absent from bannerlord-skeleton-authoring.md (grep count 0), as the plan expected. `4520af802` is left out of B4's Ported-from.
- C3 sub-step 3 (the Kit-facts row) skipped for the same reason. `2a9e8bc49` is left out of C3's Ported-from.
- Plan's Maintenance notes deferrals stand: the v1.4.8 rewrite of `8dd76ddb3`'s fief-granting and economy readings; the 1.5.x clip-flags handoff sections; later 1.5.x extensions of race-face-and-hand-morphs.md and tpac-static-prop-authoring.md; the `2ac85fbe7` gauntlet and siege-relief sections; deriving the v1.4.8 static face morph offset; WP136's hand-off of `09f3126a7`'s 'native reads a row's type' docs; a v1.4.8 Kit re-measure; WP121's modules-overview.md bin counts.
- /deep-review not run, as the task says; an independent reviewer runs next.
- No paid calls and no translation: this plan adds no player-facing strings.

### WP136

- Everything that needs WP108 B4 (D27 rule 1). tools/validate_moduledata.py has no SCHEMA_INVALID, BROKEN_FACTION_REF, ITEM_SLOT_REJECTED or TROOP_ID_REMOVED code (a grep found none), and tools/shipped_troop_ids.txt does not exist. So Steps B3, B4 and B5, and the validator half of B1, stay undone, and the whole-plan done criterion that lists shipped_troop_ids.txt is unmet. The hook --code list and test_hooks wiring that go with them are deferred too.
- PrefabSpriteTests moves to WP140 (D27 rule 1). The prepared file is C:/Users/mikew/source/repos/taom-port/136-notes/e1-wip/PrefabSpriteTests.cs and the 19 unknown sprites are listed in C:/Users/mikew/source/repos/taom-port/136-notes/e1-unknown-sprites.txt.
- .claude and docs/adrs hunks of 8caac7da5: .claude/rules/csharp-architecture.md, gui-ui.md and adapters.md, the release skill and AGENTS.md (WP143), plus Step E3 (ADR-002 and ADR-007), which stays the maintainer's. Exact edit is in plans/136-data-integrity-gates.md Step E3.
- Other stale prefab-budget lines, not edited: CLAUDE.md:187, docs/modding/module-map.md:356, docs/modding/settlements.md:398 and tools/README.md:66 still say the tool counts TAOM_Map/Prefabs only. docs/features/startup-resources.md:168 quotes the old 93,407 reading and also needs a look.
- Translation owed (no paid call was run): 24 language rows, 12 each of taom_faction_golden_realm_of_rhun_desc and taom_faction_taskralan_of_harwan_desc. Their party-size clause has no numeric token to swap, so no mechanical substitution was safe; the rows stay true but omit +5%. The 24 matching tools/translation_cache rows were left in place too. The translator reuses a cached row by string id, so those rows must be dropped when the pass runs or it will keep the old text.

### WP130 F3

- `/verify-bindings` snapshot refresh for the two new Custom Battle models (maintainer)
- Regenerating `docs/reference/ranged-troops.html` at the next ranged-ladder report run
- The Hebog-lûth horse gap: content, maintainer (D13); no Horse item was added
- Lessons, including `1641fb985`'s (WP142); reconciling `gamemodels.md`, `gui-ui.md` and `csharp-patterns.md` (WP143)
- Skill restat, RCA, lesson and generated HTML from `1641fb985`: not taken, as the plan's port table says
- Step G `/deep-review` skipped per the assignment: an independent reviewer runs next
- GitHub issue for the plan's issue-less fixes: not created; the F3 message as written in the plan has no Refs line, so none was left out

### WP115

- B1 (maintainer, M1): in the worktree run `git checkout 9ca99437fc3879c9a981d8430ff8d44873c209a0 Directory.Build.props`. It adds one target, TaomStampWorkingTreeState. Then the executor verifies and commits as the plan says, including the dirty-probe check.
- B2: --require-build transplant. Reads the B1 target. Its real-DLL check also needs B1.
- B3: scene sack, module sack and JIT reports. Its pick needs B2's copies as base. The module-sack and JIT parts do not read the target, but they cannot be cleanly separated from B2 by the step as written.
- B6 remainder, which arrives with B2: drop Gaming.Desktop from the folder tuple in TestRequireBuildCli.test_accepts_every_copy_when_all_are_clean, and drop 'Game Pass' from the SHIPPED_DLLS comment.
- B7 remainder: tag_on_another_line and TestTagOnAnotherLine (use B2's _git), and the commit-manifest read, the installed SubModule.xml comparison, TestInstalledManifest, REPO_MANIFESTS and _git_bytes (all inside require_build). Landed from B7: FIVE_X_RELEASES, release_number_problem and TestReleaseNumber.
- C5: release skill and release-process.md. Needs WP102's output plus B1, B2 and B3.
- Part B done criteria that cannot hold yet: the B1 log check, and the grep for require_build, is_module_sack and jit_optimization (retired_binaries, game_pass_bins, test_troop_files, missing_armory_shaders and release_number_problem are present).
- Part C done criterion '16 commits': 12 landed.
- M2, M3, M5 stay the maintainer's. M4: the issue for the new work (A3, A4, B7, C4) does not exist, so those commits carry no issue reference.
- /deep-review skipped as instructed; the independent reviewer runs next.

### WP121

- Part B (maintainer, Modding Kit). First step: B1, the PowerShell backup of Modules\TAOM's Assets, AssetSources and AssetPackages\pack0.tpac into C:\taom-kit-backup-<date>\TAOM. Then B2: ./build.ps1 from C:\Users\mikew\source\repos\taom-port\121. No deploying build from any tree from B6 until Part C's commits are on this branch, and until WP121 merges, deploy only from this worktree.
- Part C (ordinary session, same day as Part B), deferred whole under D27 rule 3. First step: C0, write C:\Users\mikew\source\repos\taom-port\oracle121.py from the plan and run its compare. C1 and C2 follow straight after B10, and C3 waits for the maintainer's B11 loading-screen result. C2 adds BannerAtlasFormatTests; the plan's expected counts then become Ran 7 tests, OK (skipped=4).
- Part D (maintainer, at the next 1.4.x release, D8 v2.4.0). First step: publish TAOM from the laptop install into an empty folder outside the game install, after WP121 is merged and deployed from the trunk. Then run the TAOM_PACK0 texture gate on the published pack.
- Part E (conditional, maintainer first). First step: E0, confirm one machine has a v1.4.8 Modding Kit and TAOM_Map's loose sources for the package the next 1.4.x release pins. The laptop's TAOM_Map is cooked and cannot be republished. If either is missing, record 'Part E BLOCKED: no pinned 1.4.x TAOM_Map source in a v1.4.8 Kit'.
- A4.3 /deep-review skipped per the assignment; an independent reviewer runs next. /review-codex and /codex-verify not run (paid, maintainer only).
- GitHub issue for the new work (D24 loading sheets, texture-format gate, Part E) not created: outward-facing, so it stays with the maintainer. The plan's commit messages carry no Refs line, so none was left out.

### WP140

- Part D, all of it: stale_module_files, the require_build hook, the CLI messages, tools/tests/test_package_release.py and the docs/reference/release-process.md sentence. It needs WP115's require_build, REPO_ROOT and _git. Run plan Step D1 once they exist.
- .claude/rules/gui-ui.md (D27 rule 5), Step A2 sub-step 5: the PrefabInsertPositionTests sentence after the paragraph that starts '**Why:** the four checks above are the general rule'.
- .claude/rules/gui-ui.md, Step A3 sub-step 4: the '**Hover targets:**' paragraph directly after the A2 line.
- .claude/rules/gui-ui.md, Step C3 sub-step 5: the Brush paragraph replacement and the '**Gate:**' line, exact text in the plan.
- .claude/rules/vanilla-data-comparison.md, Step B3: drop the stale '~32 of TAOM's 48 prefabs are clones' clause and replace steps 1 and 2 of 'How to fix a stale clone', exact text in the plan.
- .claude/rules/tests.md, Step C4 sub-step 4: the '## Gates that key on repo paths' section before '## Test Organization'.
- .claude/rules/moduledata-validation.md, Step C4 sub-step 5: the '## GUI prefab gates' section and table before '## Discipline'. Suggested addition, not in the plan: a row for PrefabSpriteTests.
- Because of those deferrals, A2 staged three files where the plan says four, A3 staged one where it says two, B3 has 11 D and 2 A with no M, C3 has no rule file, and C4 staged one file where it says three.
- No maintainer-only file was edited. .claude/settings*.json, Directory.Build.props and docs/adrs need no change for this WP.
- Refs lines: none written. The only reference is #371 in plan 136, and no new issue was needed.

### WP139

- Part D, Step D1 (all three files are .claude, deferred under D27 rule 5; nothing edited). Exact change when the harness WPs land: plan 139 Step D1 items 1 to 3. (1) .claude/skills/verify/SKILL.md: insert '## Step 6: Owed in-game verdicts (information only)' directly above '## Output Format', and add the 'In-game:' line after the report line that starts 'CHANGELOG:'. (2) .claude/skills/release/SKILL.md: insert the 'First, before writing a line of either note...' paragraph above the first line that starts 'Source the content from', using the ledger-yes text, and add the testers' checklist clause. (3) .claude/rules/csharp-architecture.md: add the '| Outcome lines |' and '| Outcome cost |' rows from lines 37-38 of 95647d13b, after the row for the ?. computed-properties rule. Precheck on this branch printed 0 0 0, so none of them is synced yet.
- Step 0 token '## Step 5: CHANGELOG.md untouched' printed 0 (WP102 output); waived under D25 rule 4 and D27 rule 5. D1's verify anchor depends on WP102.
- /deep-review on each Part's diff was not run: skills are unavailable to this session and the brief says an independent reviewer runs next. Probe list from the plan: the SubModule seams and their order, the CrashReportService insertion point, the C3 and C4 conflict resolutions, the baseline.
- Bookkeeping rows for WP142 (not added here, by the per-port procedure): tools/README.md row for ingame_verdict.py (1.5.x text at e45f6df2c minus its Coop and --ds-* clauses), docs/reference/feature-map.md row for docs/features/ingame-verdicts.md, and its docs/INDEX.md entry.
- Tracking issue: the 1.4.x issue the plan asks for does not exist (maintainer or orchestrator creates it). The plan's commit messages carry no Refs line, so none was left out.
- CHANGELOG.md not staged (D1). No translation or paid call was made, and this WP adds no player-facing strings.

### WP137 D

- D3 /verify hunk in .claude/skills/verify/SKILL.md: '## Step 2d: API snapshot reproduces' (from 6ae924638) and the output line 'API snapshot: [REPRODUCES/DRIFT/NOT RUN]'. Deferred by this run's scope. D3's greps for those two tokens were therefore not satisfied, and WP138's K7 reads the Step 2d token, so K7 stays false until it lands. The 'dotnet test ... --no-build' grep was not run: it only guards Step 2d's wording, which did not land.
- D1 hunk in .claude/skills/engine-bump/SKILL.md, Phase 4 item 1 ('-Check reports DRIFT ... by design' becomes 'exits 1 with DRIFT, printing up to 20 differing lines ...'). My call: the task text is silent on this hunk, but the plans/README.md integrator record lists D1's .claude hunk among those landing as a set after WP103. Until then the skill's sentence is stale. The plan's D1 regex for it was not run.
- D2 item 2 (PathReuseCache catalogue move): skipped as the plan directs, because the count in ReflectionSiteBindingTests.cs is 0 and WP127 already removed the row. Test Total stays 13355.

### WP122

- C2 (Ruff gate and Claude config lint): STOP. Failed preconditions: `python -m ruff --version` prints `No module named ruff` (M1); `grep -c CLAUDE_PROJECT_DIR .claude/settings.json` prints 0, needs 20 or more (WP103, a second blocker besides M1). The third precondition holds: the harness-facts row count is 1.
- C3 (ShellCheck gate): STOP. `shellcheck: command not found` and TAOM_SHELLCHECK is unset (M1).
- C4 (NuGet audit): waits for M2. `grep -c NuGetAuditMode Directory.Build.props` prints 0. Exact change for the maintainer: the M2 block in plans/122-test-infrastructure-gates.md, 'Maintainer steps' (NuGetAuditMode all, WarningsAsErrors NU1903 and NU1904), inserted after `<Nullable>enable</Nullable>` and its blank line. Directory.Build.props was not touched. WP115 B1's build-identity comment is still absent from that file, so M2's 'before the Build identity comment' anchor applies only once B1 lands; until then insert it after the Nullable line. The executor then runs C4 steps 2 and 3.
- C5 (Gitleaks history scan): maintainer only (M3).
- Part D (nullable ratchet): not started, per D27 rule 4.
- Step 0 tokens waived: WP115 (TaomStampWorkingTreeState, B1 deferred) and GA-SAME (D28). The GameAssemblies diff from 7f02fc8d3 was confirmed as the one comment line, and A1.2 applied the series diff. `git grep PathReuseCache` over TAOM.Tests and Main prints nothing.

### WP145 A-D

- .claude/rules/gamemodels.md (NOT config-protected, but .claude hunks are deferred for this run; plan C2 did not name this file). Delete line 103: `| `TaomHeroCreationModel` | `DefaultHeroCreationModel` | `RaceAge` |`. Change line 9 from `TAOM has 50 GameModel overrides (49 registered + 1 abstract base).` to `TAOM has 49 GameModel overrides (48 registered + 1 abstract base).` This should clear the new test_the_real_catalogues_are_in_sync failure and both model_registry lint findings (not run here, because the edit is deferred). It should land with or straight after this branch.
- Part E (needs WP108 B4, WP138, WP143): untouched, per D29.
- Part F (needs WP116, WP138, WP143): untouched, per D29.
- Part G (needs WP142, WP144 follow-ups, Parts B to F): untouched, per D29. This includes G6: patch-targets.md and gamemodel-bases.md still list the two deleted classes until the snapshot is regenerated, and G7's career, banner and offspring doc follow-ups.
- GitHub issue for Part A: it does not exist, so no Refs line was written (D29 rule 3). The issue stays owed.
- Conflicted doc hunks dropped from docs/features/mixed-formations.md, as C5 allows: the hotkey table row (35044f583) and the per-frame bullet (35aa37439 and 35044f583). They describe CachedEnumParse and CycleHotkeyParser, which grep shows are absent on this line. The 'No allocations in the hot path' sentence therefore stays uncorrected. The Debug Mode row in the same conflict was written by hand so it matches the new hint.
- Maintainer steps: push, merge and any tag; `/deep-review` and `/verify` recommended to the integrator (an independent reviewer runs next). `/security-scan` is only asked for after Part F, so it is not owed yet.

## In-game checks owed, later waves

### WP127

- Damage model (Part B): start or load a campaign, then fight a field battle with cavalry charges and a creature. Charges still knock infantry down; a cave or hill troll still crushes through a non-shield block; no exception in taom_debug_*.log names CombatMechanicsHooks, RefugeDamageHooks or TaomCombatMechanicsModel; no [CrashReport] capture. If a refuge is available, defend it: defenders still take visibly less damage.
- Supply Lines (A2): place one order paid to a lord and one paid to a settlement, and fail or cancel one to trigger the refund. Gold and refunded troops move as before; no `[SupplyLines] charge: ... unreachable` or `[SupplyLines] refund: ... unreachable` warning for a reachable payee.
- Refuge and warden (A2): make peace with a held lord's faction and he is released. Promote a warden and the companion appears, with no `[Refuge] promoted-warden rename failed` or `[Refuge] promotion unwind failed` line. A hostile party near a refuge is raided nearest-first as before.
- Keep-outs (A2): making camp, and founding a refuge, next to a town or castle are both refused as before.

### WP128

- Battle exits (#827): join a commander's battle through his army; the encounter menu shows neither 'Abandon army' nor 'Leave'. Kingdomless commander (merge fails): no 'Leave'. Safe-passage truce with the enemy faction and no army: 'Leave' shows again, and after it the state returns to EnlistedAttached once the commander's battle ends.
- Discharge during load (#828): switch Enable Enlistment off in MCM, save on the map with the service menu open, load. Body reads 'Your service has ended.' with one option 'Go on your way', landing on the map. Repeat once in German (English seed text shows).
- Raids (#831, #954): serve a lord who raids a village; taom.service_status stays EnlistedAttached, the board says 'The column is raiding.', release can be asked. A raid on the lord's own village he defends is joined. A lord with troops relieving the raided village: the soldier joins, then returns to normal service.
- Wars (#832, #960): as an independent clan, enlist, let the lord's realm declare a new war, join a battle against that faction; log shows 'topped up the war with '<id>' before a join', and after discharge 'service war with '<id>' ended'. As a vassal of another kingdom, lord fighting a faction your kingdom is at peace with: 'sitting this battle out', no new war.
- Merit (#834): fall early in an enlisted field battle, take over a soldier, kill a few enemies; log line 'battle merit ... (kills=N, won=...)' counts only the hero's own kills.
- Encounter exits (#1021): defend the lord's town until the attackers retreat; within three in-game hours the log shows 'releasing a stale EnlistedBattle latch inside a settlement', the next assault logs 'cleared the last battle's result from the reused encounter (#1021)' then 'joined commander battle on side Defender', and that assault is fought. Save at an assault's encounter menu as a defender, switch Enlistment off, load: 'feature disabled: waiting for the battle to end before releasing', then the release after the battle.
- Duty text (#845): in English, ask for work until a field duty is assigned; the order toast and the board name the duty ('a reconnaissance sweep') and its result toast is the duty's own line. Night patrol, an incident and their choices read as before. Repeat in Deutsch. The log has no 'duty '...' skipped' line.
- Cleanups (#842 to #844): an existing save with an enlisted soldier loads; duties are still offered by the column's state; battle pay, trust and promotion work as before.
- Logs: Logs/taom_debug_*.log, lines tagged [Enlistment] and [EnlistDiag]; console commands taom.service_status and taom.rescue_service.

### WP129

- Log: newest Logs\taom_debug_<timestamp>.log beside the game executable (bin\Win64_Shipping_Client). Use a campaign with a career hero; taom.career_perks needs cheats on.
- Mount bonuses (#611): Cavalry career with a MountChargeDamage pick. In battle taom.career_perks shows the mount's MountChargeDamage above base and '[CareerPerks] mount stats for rider' appears once. Fire the Cavalry ability: mount speed and charge rise in the report, then fall back when the buff ends.
- Typed resistance (#613): black_uruk_captain with black_uruk_captain_t1_a_p2 (Blunt). 'hit reduction' lines appear for mace and horse-charge hits, not for sword cuts.
- Ammo (#613): a javelin career with an Ammo pick spawns with more javelins than the printed maximum; '[CareerPerks] Ammo +N%' lines show amount and max before and after.
- Hit summary (A3): a hero with a TroopDamage pick fights a battle. One 'hit amp' DEBUG line per combination, then at mission end '[CareerPerks] hit summary for this mission: ...' with a hit count.
- Groups (#766): at level 1 the career screen refuses a tier 3 choice; a save with a foreign choice shows one 'Career choices that belonged to another career were refunded' message on load.
- Earnings (#767, #770, #790): with captain_of_osgiliath_t3_a_p4 (+10% gain), a battle, raid and hideout each log a [SpecRes] earning 10% above base; a militia-only raid pays the raid credit once and no battle credit; a send-troops hideout clear writes one '[SpecRes] HIDEOUT:' line, also after saving in the success menu and loading.
- Party size (#768): take a PartySize pick. The party limit in the party screen rises at once; an owned caravan's limit does not. taom.career_perks prints the tooltip value, the enforcement value and the engine cache, all equal.
- Promotions (#612): an Isengard or Mordor campaign with Field Commission on gets a promotion offer after a won fair battle; no '[FieldCommission] allowedRaceNames entry' warning at start.
- Archetypes: a goblin, Lindon or Abanissa career's ability applies its buff (damage, speed or mount line in taom.career_perks while it runs).
- Career button (#971): one click sound, and the career screen opens.
- Heir quest: let the hero die with a career quest running and continue as the heir. The old quest shows cancelled with vanilla's clan-leader text, 'CareerQuest: cancelled ...' is in the log, and the heir is offered their own career's quest.
- Career screen (#769 fixes): the header shows the root perk line under the description; a MountHealth pick shows '+10% mount health'; a keystone with a passive adds its value to the Active Effects total.

### WP130

- Campaign battle with a cave troll unit and banners: no troll carries a banner; kill a bearer next to a troll and watch the dropped banner fetched by a non-troll or left lying.
- Custom Battle with Mordor (trolls): no troll raises or fetches a banner.
- One shader pre-compile batch (it runs hidden Custom Battles): no exception naming TaomCustomBattleBannerBearersModel or TaomCustomBattleDamageModel.
- Dread: a campaign battle against a Nazgul: only troops in contact weaken; no formation-wide rout from the aura alone; MCM 'Dread Radius' shows 5 on an install with an old TAOM.json.
- Knockdown: Rohan cavalry into Mordor orcs and uruks floors them head-on and floors men from any angle at full speed; cave trolls and dwarves mostly stay up; MCM 'Auto-Knockdown Weight Ratio' shows 6 on an old TAOM.json.
- Custom Battle combat (Part D): troll cleave and stagger immunity, crush-through and charge knockdown work; a mounted hit causes no crash.
- Battle action bar: the #970 checklist in docs/features/companion-tactics.md; with the setting off (default) no bar and no input change; the [BattleLoad] TAOM-behaviour count is two lower.
- Warg: a player on a warg still loses and regains control in a rage; warg reinforcements return to their formation after a rage or after their warg dies.
- taom.spawn_troops <id> 5 enemy in a Custom Battle (cheat mode) spawns troops.
- Dunland (owed only once F3 lands): the Hunter fights with the archers and the Hebog-luth with the horse archers; check whether the Hebog-luth are mounted, since their battle rosters have no Horse item (report it, do not add one).

### WP131

- Startup: no main-menu inquiry about failed patch categories; the debug log has no '[Module] AlignmentRecruitment failed', no '[Module] MessengerSessionRules failed', no '[CastleRecruitment] AiHourlyTick:' warning, no '[CastleRecruitment] ... not found' and no 'CheckRecruiting bind failed' after loading a campaign.
- Troop weight: an elven (Rivendell or Mirkwood) castle garrison over about a month: no daily desertion of elite troops; party and clan screens show raw count against the real limit; an elite patrol is not slowed by an over-limit penalty.
- Castle recruitment: AI lords ride to castles only when castle notables hold volunteers; an AI lord parked in a castle tops up hourly; with 'AI Recruits From Castles' OFF an AI lord entering a castle takes no castle volunteers while garrisons still recruit daily; ON regresses nothing; kingdom lords far from home no longer target friendly foreign castles.
- Alignment scoring: with 'Apply To AI Lords' on, a Free lord near an Evil town at peace does not plan a recruiting trip there (food and trade visits still happen); no '[AlignmentRecruitment] Patch114 postfix failed' line.
- Messenger button: on a hero's encyclopedia page Send Messenger has the popup button look, its label dims when greyed, and hovering a greyed button shows the reason.
- Messenger session rules: run the eight-item 'In-game Checklist (#898)' in docs/features/messengers.md.

### WP132

- Survival (A1): enlist with a hero who has good Medicine and fall in a few of the commander's battles. Expect no error from TaomPartyHealingModel and deaths no likelier than when freelancing.
- Quartermaster (#829): ask the commander for kit twice at one rank. Expect the issued line, then 'You have drawn your due for your rank already'. In German the three new lines read in English until WP135.
- Sit-out (#830), not-lopsided field battle: 'Leave it to the others' is greyed with 'You serve in your commander's line...' while Attack is enabled.
- Sit-out (#830), wounded: the option is enabled with the wounded tooltip; after the battle 'You were wounded and stayed back...' shows and no renown is paid.
- Sit-out (#830), about 85 percent strength or more on his side: the option is enabled with the odds tooltip; afterwards the log shows '[Enlistment] sat out a simulated battle (OverwhelmingCost, ...)', half base XP and one trust lost.
- Sit-out (#830), siege assault: the option is greyed with the siege tooltip.
- Board pay (#839): while the commander is lost (grace state), 'Daily pay' reads 0.
- Pump clock (#841): on the service menu the board updates with time running, while paused and right after loading onto the menu. A commander relieving a raided village, joined from the running service menu, opens the battle on the next frame with no casualties taken first (trace on: 'join into a raid deferred to the wait-menu frame'); a field battle joins at once.
- Siege duty (#1014): with the commander defending a besieged town, ask for work. The siege duty (walking the works) can be offered and the quiet night watch is no longer the only kind.
- Battle rule (#796): after a battle's loot screens, service returns to normal with no stuck state; taom.service_status reads EnlistedAttached.
- Logs: <game>/bin/Win64_Shipping_Client/Logs/taom_debug_*.log, lines tagged [Enlistment]; [EnlistDiag] lines need the enlistment trace on in MCM.

### WP133

- Inventory, Quick Actions, Unequip All: slots empty, items in list, doll, armour, weight, capacity update; Reset then Cancel restore gear; no '[QuickActions] inventory panel refresh failed' line.
- Load a preset with the inventory open: doll and stats update at once; a preset equipping the last of a stack removes the row; no '[EquipPresets] inventory panel refresh failed' line.
- Sell Low Value on a whole stack: row disappears at once. MCM: 'Sell Damaged Equipped' and 'Sell Low Value Equipped' gone.
- Banner Color Persistence on, custom clan colours: inventory doll shows clan colours on open, outfit toggle, Reset and preset load; a stealth set keeps the vanilla stealth colour.
- Portraits show clan colours on the party screen, clan screen, hero panels and the Order of Battle hero pool, AI lords included.
- Clan screen with a refuge being raised: row keeps '(being raised)' after the screen opens, wage slider hidden, wage line reads 0.
- Field battle, Lindon and Blue Craig formations with no lord: bearers raise the archers' flag and the orcs' standard. Owed only after B2 lands; it will fail on this branch by design.
- Load a campaign: special resource counter appears with the map bar at once; switch to or become a hero with no resource: the counter leaves.
- F6 with the escape menu open and while a prisoner: nothing opens; normal F6 opens the fief hub; Make camp disabled while a menu is open.
- Watch several towns' markets over days: no troll gear or other not-for-sale item in new stock. Only meaningful for items the installed Armory marks; re-check on the public Armory.
- Tavern maid in a Gondor town: 'a mug of ale with a bowl of stew'; in a Rohan town: vanilla's dishes.
- Coronation in a TAOM-only realm shows its title (no 'ERROR: Text with id' in the popup or rgl_log); Dunland's says 'Brenin of Dunland'; first talk with a Gondor lord names 'Lord of Gondor'.

### WP134

- Gondor's policies: in a new campaign, Kingdom > Policies for Gondor shows Land Grants for Veterans active beside Senate. No log line is expected.
- Alignment sides, as a Gondor-cultured vassal of Umbar or Khand: (a) a Mordor-culture wanderer's hire goes ahead; (b) on the party screen a Rohan prisoner reads -1 and a Gondor prisoner 0; (c) optional, an execution's pre-confirm relation preview agrees with the applied changes. The newest taom_debug log must have no '[WandererAllegiance] could not evaluate the hire verdict' error.
- Wanderers: a goblin, Misty Mountain orc, Blue Craig or Lindon wanderer in a tavern gives an introduction and backstory, with no 'ERROR: Text with id backstory_c doesn't exist!'.
- Owed only once the deferred steps land: Lindon party-screen weights (plan check 2), generated lords' kits (check 5), lesser Nazgul kit and Custom Battle (check 6).

### WP135

- #706, German: an aom_ notable or a TAOM_ minor faction shows its German row. Switching to English logs '[LocalizationOverride] Text language is English again: the English string overrides apply'; back to German logs "Text language 'Deutsch' is not English". A session that starts in English logs neither. Read Logs/taom_debug_<timestamp>.log under bin/Win64_Shipping_Client.
- #1026, with TAOM loaded after TAOM_Map and the Armory: English party names read "<Name>'s Party", and a German or Turkish line with a lord's name keeps its article or suffix. With TAOM_Map loaded after TAOM, seven languages lose this until M2 is done.
- Names, after the fill and C3: in German, party screen troop names, encyclopedia lord names, clan and kingdom names are German. A NEW campaign shows six Umbar clan names; an existing campaign keeps its saved one.
- Vanilla rewordings (B2): German 'Call ... to war' decisions read the game's own German; a kept BR row such as 0cdXddA9 still shows TAOM's row.
- Custom Battle: no 'Text with id' error for a TAOM culture text (A3).
- A translated conversation line with an animation tag animates and shows no [ib:...] text (A9).
- Keys flagged by loc_length_report.py in fixed-width widgets fit in that language (C4). Run on A10 with --since e3a34b651: 23 of 139 keys flagged, for example taom_wsc_t1_a_p2 at FR ratio 1.40.
- A German Combat issue without its own texts reads the 'a foe' fallback text (A8).

### WP137

- C2: after a build with this branch runs the game, <game>\Modules\TAOM.Dependencies\diag.log shows the OnSubModuleLoad shield sequence with CollectAssemblyTypesShim.Install followed by PatchShield.Install (pass 1) and no SubModuleConstructionGuard line. The main menu opens and a save loads.
- E2: press Play in Visual Studio with the Bannerlord profile; only the managed debugger attaches. Compare the PatchShield pass 2 duration in diag.log with an earlier mixed-mode run (the speed-up is UNVERIFIED).
- E4 (desktop): pwsh tools/dump_engine_skeleton.ps1 -List and one extract_human_anims_tpac.ps1 run with TpacTool v0.5.1 (skeleton body positions are Vector4; the extractor needs win-x64\native\assimp.dll in that folder, UNVERIFIED).
- Next freeze or hang report: follow /native-crash-triage Phase 2b on the desktop once B4 lands, and record whether the frames named the stuck loop.

### WP141

- No gameplay in-game check is owed: no ModuleData or player-loaded file changed (git diff against the base for Main/_Module and CHANGELOG.md is empty).
- First real use on the desktop, reading the dry run first: Apply-MapVillageNames.py --apply after a rename. A DE client then shows the new English name until a translator run; switch language to see it refresh.
- First real use on the desktop: the starter kit and ranged ladder generators without --asset-repo, confirming the lotraom-assets mirror stays untouched.
- First real use on the desktop: check_rdc_entries.py against the loose Armory. This laptop's installs are cooked, so it reports no RuntimeDataCache folder here.
- After D1 steps 3 and 4 and D2: a raw 'graphify update' from both shell tools must be denied in a live session.

### WP144

- Camp prefabs (B5): start a Field camp on the 1.4.8 build; in taom_debug_*.log look for `[FieldCamp] placed 1 Field camp entities` (prefab) or a larger count (fallback); same for a refuge (`[Refuge] placed N`). Record which in tpac-static-prop-authoring.md.
- Elephant skeleton package (A7): on the desktop's loose Armory, confirm adod_elephant_geo.tpac carries elephant_skeleton and elephant_harad_armor_01_geo.tpac is a 416-byte stub.
- Hidden inner gates (C1): one campaign siege at taom_rohan_castle_helms_deep_forceatmo or taom_gondor_castle_001_forceatmo: does the middle lane ever open, or do attackers stand at the gate?
- Female dwarf meshes (A8): for the Armory a 1.4.x release pairs with, read skins.xml's dwarf woman face_meta_mesh. The installed v2.0.26 reads sm_dwarf_basemesh_a1_head (the stopgap). If a paired build reads sk_dwarf_bm_f1_head, look at a female dwarf lord close up for the #385 crash and the eye gap.
- Map-view GPU memory (B4): on the map, open and close the inventory 20 times; watch dedicated GPU memory with Release Map View Memory on and off.
- Being-struck band (A1): optional; a war ram head-butt shows no flinch.
- Tournament armour (A4): in a tournament match fighters wear their culture's practice kit; in an arena practice fight each fighter wears his own culture's kit too.

### WP136

- Mordor's card: character creation culture stage and the faction screen show +20% party size limit, in English and one other language.
- Rhun and Harwan cards: the English description now reads +5% party size; no other language changed.
- Seven banners in the encyclopedia: Nos Alenwe, Nos Iminye, Zorian, the Rivendell and Lothlorien houses, Torabai, Khorvash. No new banner line in rgl_log.
- Three removed hero templates: start a campaign; no new Null object reference lines in rgl_log_errors compared with a build before this WP.
- python tools/validate_xml_schemas.py --live on the public Armory after the WP101 refresh. Today, on the laptop's Armory v2.0.26, it prints FAIL: 215 file(s) validated, 1 failed (LOTRAOM_horses.xml, family_type). That is the old Armory, not a verdict.
- Decide the taom_canonical_lord_I1_0_skills drift between generator and file before any --apply. Today apply_culture_skills_traits.py --check prints OK: 145 SkillSets match the generator.

### WP130 F3

- Check 10 (the one F3 owes): in a battle, the Dunland Hunter fights with the archers and the Hebog-lûth with the horse archers. Also check whether the Hebog-lûth are mounted: the plan says their battle rosters have no Horse item, so they may fight on foot in the horse archer formation. A horse is content (D13): report it, do not add one.
- Plan 130's checks 1 to 9 stay owed from the earlier parts (D26).

### WP115

- Engine line: Logs/taom_debug_*.log top lines show `[Engine] Bannerlord=v1.4.8... modules(N)=[...]` beside [BuildStamp], naming TAOM_Map and LOTRLOME_Armory with versions.
- BuildStamp tokens: `config=Debug jitOpt=OFF` after each stamp, and `(pair OK: both built from <sha8>)` for a paired build.
- Shutdown line: end of the log shows `[Shutdown] clean module unload` after quit to desktop, and it is absent after killing the process.
- Crash bundle: trigger with the QA dev trigger in MCM. The zip holds diag.log, report.txt Identity has `Build:`, manifest.txt has `TAOM build:`.
- Log retention: Logs/ after more than 10 launches keeps up to 30 taom_debug_*.log.
- Game Pass target: ./build.ps1 in a checkout still holding the staging folder fails with the Game Pass message, then succeeds after M2, and no Gaming.Desktop folder reappears under <game>/Modules/TAOM/bin.
- Release gate (owed once B1 to B3 and C5 land): Phase 8 dry runs on the install and on the channel folder exit 0 with `build stamp OK`, and the pairing check exits 0.

### WP121

- B5: the Kit opens TAOM with no 'Cannot find' or dependency-conflict box. diag.log has 'RunEarlyPhase: loaded by the Modding Kit; skipping crash-loop detection'. No session-launching.marker after the Kit closes. The next game's diag.log reads previousCrashLoop=False.
- B2: the deploying build logs 'TAOM.Dependencies: mirrored N files Win64_Shipping_Client -> Win64_Shipping_wEditor' and the two bin folders hold equal file counts.
- B7: Abanissa kingdom and clan banners show solid sigils in the Encyclopedia and on the map.
- B11: banners on the map, in the Encyclopedia and in one battle, covering Gundabad, Harad, Rhun, Mordor and Rohan clans. Goblin and Misty Mountain Orc icons in the banner editor preview. The banner editor icon list unchanged.
- B11 (if B9 was done): three or four loading screens without banding or blockiness.
- B4 and B11: [MemStation] enter screen='MapScreen' and the next [MemSample] in taom_debug_*.log, plus Dedicated GPU memory, before and after. This is a record, not a gate (D23).
- Part D: the TAOM_PACK0 run passes with no skip; banners and loading screens checked on the packaged build.
- Part E: the map horizon after the vista change; check_map_vista.py exits 0 on the pinned TAOM_Map.

### WP140

- Deploy with this branch merged, and first remove the 19 deleted files from <game>/Modules/TAOM/GUI/Prefabs/ (or reinstall the module). Watch rgl_log_*.txt for 'Couldn't find Custom Widget type', 'builtin widget type not found', 'This prefab has already been added' and 'Failed to apply extension' lines naming a TAOM screen.
- Party screen, troop upgrade and recruit pop-up: hovering the horse count shows the used-horses tooltip; header columns line up with the rows; the single centred Done button and the top-right close button work; with a gamepad, key hints appear beside the hovered row's buttons.
- Party screen sort bar, composition panel and troop rows; custom battle army sliders, dropdown and troop type pop-up; campaign party and settlement nameplates: all unchanged (they now load the identical vanilla files).
- Battle: the career energy bar sits above the health cluster as before.
- Battle action bar (if enabled): the formation name and button labels use the tooltip text style.
- Career screen: switch-mode subtitle, no-targets message, career names, descriptions and ability names use the career screen styles instead of the default font.
- Custom battle: the army size slider shows vanilla's track, fill, frame and knob, and the number box has its frame.
- Inventory: the Presets button label uses the popup button text style.
- Character creation narrative stage, custom battle screen panels, order-of-battle overlay buttons and party screen header buttons look and behave as before.

### WP139

- Session summary: play 12 minutes, save, quit to desktop. In <game>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log expect [Session] summary reason=heartbeat after about 10 minutes, reason=save just before the save's lines, reason=exit just before [Shutdown] clean module unload, with shield_swallowed= present.
- Crash summary: any captured crash or the dev crash trigger. The bundle's report.txt and taom_debug.log show [Session] summary reason=crash before the log tail.
- #970: Show Battle Action Bar on, field battle, Begin battle, Esc, click a stance. python tools/ingame_verdict.py --issue 970 gives PASS for attach and stance.
- #971: Character screen, career button. --issue 971 gives PASS for open and init.
- #1011: save a campaign, load it in the next session. --issue 1011 --sessions 2 gives PASS for capture (dead= above 0) and restore.
- #918: Castle Recruitment on, AI Recruits From Castles off; fresh launch, one load, three days. --issue 918 gives PASS (three days with blocked= 1 or more).
- #1076: only on a game with a hollow parent. --issue 1076; NOT REACHED is normal in a healthy game.
- Swallowed sites: normal play. At most one [Swallowed] WARNING per site in the log, repeats counted in [Session] swallowed lines, no behaviour change.
- /verify Step 6: run /verify after a session once Part D lands. Expect an In-game: line and nothing blocking.
- All of these need a build deployed with ./build.ps1 from a clean tree that contains these commits. Never pass --post without the maintainer's word.

### WP137 D

- Plan 137's in-game checks are unchanged by Part D and remain owed: (1) diag.log shield sequence after a C2 build, (2) E2 Visual Studio Play with the Bannerlord profile, (3) E4 on the desktop with TpacTool v0.5.1, (4) /native-crash-triage Phase 2b at the next freeze report. Part D itself adds none (tool, docs and one comment).

### WP122

- None for Parts A to C: no game code changed (`git diff --name-only port148/overnight..HEAD -- Main` is empty). The plan's two Siege checks belong to Part D, which was not run.

### WP145 A-D

- Warg battle (Part A): field battle with wargs under AI riders, long enough for rages. Newest taom_debug_*.log must have no `Exception running the behavior tree` line. Riders return to formation after a rage (watch one, or a rider who dismounts mid-rage). A rider who chases out of reach is released about 6 s after the last attack. Rages now end sooner, always after 2 attacks with no roll: say if that feels wrong.
- Career screen (B1, B2): a `goblin_troll_driver` or `falathrim_mariner` career shows an icon. Hover a diamond: the tooltip text is readable, with the frame under the canvas.
- Clan screen (C3): with clan colours on, a party row's hero portrait keeps clan colours.
- Lords' children (C2): a child born in a campaign takes the same-sex parent's race and look.
- Nameplates (C4): change a fade slider in MCM on the map; the fade follows it, after a clean launch too.
- Game menu and party screen (D3): both open and draw as before, the party screen's formation area in particular.
- Mixed Formations (C5): the new `[MixedFormations] layout suspended` log line appears once per formation when a formation turns cavalry, and the Debug Mode hint text reads correctly in MCM.
