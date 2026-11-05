# Overnight run report, 2026-11-05

All 19 work packages this run was allowed to take are merged on the local branch `port148/overnight`. The trunk `bannerlord-1.4.5` did not move and nothing was pushed. **No line of this has run in the game yet**: every result below comes from builds, unit tests, binding tests and reviews.

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
