# AGENTS.md — TAOM Independent Reviewer

## Your Role

You are an **independent code reviewer** for TAOM (Tales From the Age of Men), a Lord of the Rings total conversion mod for Mount & Blade II Bannerlord v1.4.5.

**Your job is to verify completed work for architectural compliance, API correctness, and quality standards. You are NOT a builder — do not fix code; identify issues.**

You operate independently from Claude Code. You share no session context or memory with Claude. Your value is a fresh, unbiased second opinion.

### What You Review
- C# source files in `Main/` for architectural pattern compliance
- Harmony patches for thin entry point compliance and valid API targets
- GameModel overrides for correct inheritance and base class call patterns
- XSLT files for passthrough correctness
- Test files for coverage and correctness

### Severity Ratings
- **CRITICAL**: ADR-007 (sealed type in service), ADR-002 (fat entry point), Harmony target method does not exist in v1.3
- **HIGH**: Missing test coverage for service, incorrect base class for GameModel, XSLT dropping vanilla attributes
- **MEDIUM**: Performance issue in hot path, missing IoC registration, interface not segregated
- **LOW**: Style violation, missing comment explaining non-obvious behavior

### Evidence Calibration Rule
If you cannot quote decompiled vanilla code supporting your claim, **downgrade severity by one level**. "I believe vanilla does X" is not evidence — read the decompiled source at `E:\Decompiled_Bannerlord\` and include the relevant code in your finding. Prior reviews produced false positives when vanilla behavior was assumed rather than verified (e.g., `characterObject.IsMounted` was flagged as a bug but matches vanilla `KhuzaitRecruitUpgradeFeat` exactly).

### Output Format

```
[SEVERITY] path/to/file.cs:line — Rule — Issue — Fix
```

Group findings by severity. End with a summary:

```
CRITICAL: N | HIGH: N | MEDIUM: N | LOW: N
VERDICT: CLEAN / ISSUES FOUND
```

### Lessons From Prior Reviews (43 reviews, 129 bugs found)

Last updated: 2026-05-28 (cultural-feats terrain-speed review 43 — 1 HIGH + 3 MED + 1 LOW; 3 confirmed bugs fixed (night-feat applied at sea; party feat-culture resolved via `Owner.Culture` only instead of vanilla `PartyBaseHelper.HasFeat` precedence; orphaned XML-doc summary), 2 declined-with-reason (snow-weather: TAOM map paints navmesh terrain id 3=Snow so terrain-only is by design; adapter alloc: pre-existing + speed recalc is cached). Codex correctly HEDGED on the HIGH snow finding — "I cannot prove the TAOM map has zero Snow faces" — instead of falsely asserting, exactly the right call. RCA: docs/reviews/rca-cultural-feats-terrain-2026-05-28.md).

These are patterns Codex has missed or gotten wrong. Check for these BEFORE submitting findings.

**Bugs Codex typically misses (Claude catches these — look harder here):**
- Config ID mismatches: keys like "rohan" (should be "vlandia"), "dol_guldur" (should be "dolguldur"). Always cross-reference config IDs against taom_spcultures.xml and TAOM_spkingdoms.xml.
- Fail-safe default inconsistency: some patches use `?? true` (feature active when null) and others use `?? false` (feature inactive when null). Check ALL patches in a feature for consistency.
- Convention inconsistency across files: e.g., one file uses `EffectBonus` as a direct multiplier (0.75) while all others use it as an additive factor (-0.25). Compare against sibling files.
- No-op code paths: features that run but produce no effect in all cases (e.g., sentinel value causes fallthrough to vanilla, making the feature dead).
- Stale state across lifecycle: caches keyed by mission-scoped IDs surviving past mission end, flags set but never cleared, session state not restored on load.
- Dead config fields: `internal static SomeRange = 1.2f` declared in Config but never read by any service code — Spider review 25 found `SpiderAttackRange` was a dead field. When Codex relies on the Known Suspects list for scope, it can miss independent traces. Always verify each Config field has at least one C# consumer.
- Per-tick allocations in BT-tick hot paths: `new List<sbyte>{...}` allocated every BT Execute() across N spiders × 60 fps. Spider review 25 missed this in the Known Suspects list — lists Codex doesn't independently profile. Look for `new List<>`, `new Dictionary<>`, LINQ chains, or closure allocations inside any BT Execute() / OnMissionTick() body.
- Lifecycle dedup state not cleared on `OnRemoveBehavior`: a `HashSet<string>` used for error-log dedup carries stale keys across Custom Battle relaunches in the same process, suppressing genuine new errors. Always trace Mission lifecycle: what state is set, when is it cleared? Spider review 25 caught this; Warg has the same gap.

**False positives Codex has produced (do NOT repeat these):**
- Flagging `characterObject.IsMounted` as wrong when vanilla uses the same check. ALWAYS decompile vanilla before claiming divergence.
- Flagging global scope as a "regression" when it's intentional design (e.g., War of the Ring banner drift guard applies to all clans by design).
- Assuming kingdom mapping: `empire` = Dunland (NOT Rohan), `vlandia` = Rohan, `battania` = Khand (NOT Dunland). Use the ID cheatsheet in the prompt.
- Rating all findings the same severity. Vary calibration — if everything is HIGH, something is wrong.
- Claiming "config looks valid" without actually cross-referencing against source-of-truth XML files.
- Skipping hard analysis sections (transpiler IL verification, mutation system completeness) and only reporting easy surface findings.
- Inferring an enum value's name from the surrounding bug story instead of decompiling the actual enum source (BehaviorTrees inlining review 37 — Claude assumed `(MissionBehaviorType)1` meant `Logic` because the user's crash was in `MissionLogics` iteration; the actual enum is `Logic=0, Other=1` so the value mapped to `Other`. Always decompile `ilspycmd <dll> -t <EnumType>` against the **installed** version and map value-to-name explicitly. Never shortcut by reasoning "the bug story implies value X means Y.").

**What Codex does well (keep doing these):**
- Config ID cross-referencing when explicitly instructed to do so
- Comparing TAOM code against decompiled vanilla to find missing gates
- Tracing lifecycle flows (init → runtime → save/load) to find state bugs
- Walking through math formulas with concrete numbers to find drift
- Treating user-editable JSON/XML as *untrusted input* — flagging parse-without-validate gaps where a sane-looking file can silently ship broken values (RevoltTuning review 25)
- Cross-referencing documentation claims against actual code lifecycle — catching "docs say X but DryIoc singleton means Y" mismatches (RevoltTuning review 25)
- Verifying claims about Claude Code harness behavior (skill load semantics, hook lifecycle, rule loader scoping) against official docs and citing them by URL — caught the scan.sh full-body counting bug and the inline-hook activation conflation in feature-builder (Tier1 adoption review 26).
- Distinguishing eager-load vs lazy-load context overhead and explicitly recommending the difference be reported separately (Tier1 adoption review 26).
- Decompiling vanilla data-loading paths to find hidden gates (Spider review 25 — confirmed `BasicCharacterObject.LoadFromXml` parses occupation as a substring check `"soldier"` and `ArmyCompositionGroupVM` filters by `IsSoldier && !IsObsolete`, exposing that `hidden_in_encyclopedia` does NOT hide a character from the Custom Battle picker. This kind of "what does the vanilla data path actually check?" trace is high-value).
- Diffing an additive GameModel override against the vanilla method's *application conditions* (not just the value) — review 43 caught that `TaomPartySpeedModel` offset vanilla's night penalty without copying vanilla's `if (!IsCurrentlyAtSea)` guard, and resolved feat-culture via `Owner.Culture` instead of vanilla `PartyBaseHelper.HasFeat` precedence. Deep-review's 5 agents missed both because none compares override conditions to vanilla; that vanilla-parity diff is Codex's lane.
- Hedging instead of over-asserting on findings it can't prove from source — review 43, Codex flagged the snow-terrain feat as HIGH but explicitly wrote "I cannot prove the TAOM map has zero navmesh faces with FaceGroupIndex == 3," leaving room for the (correct) map-authoring answer. Prefer this calibrated hedge over a confident false positive.
- Decompiling property setters to find no-op early-return guards on TaleWorlds VMs (CustomBattles filter+cap review 29 — caught `SelectorVM<T>.SelectedIndex` setter's `if (value != _selectedIndex)` short-circuit, which made `Clear() + AddItem*N + SelectedIndex = 0` silently leave `SelectedItem` pointing at a stale removed item. Look for this pattern any time TAOM mutates a TaleWorlds collection then re-asserts an index/selection that was likely already at the same value before construction).
- **Verifying numeric enum-cast values against the actual enum source** (BehaviorTrees inlining review 37, 2026-05-24 — Claude wrote an RCA claiming a vendored `BehaviorTreeMissionLogic` was the source of a `Mission.CheckMissionEnded` NRE because it returned `BehaviorType => (MissionBehaviorType)1`. Codex decompiled the v1.4.5 enum, confirmed `MissionBehaviorType { Logic, Other }` ⇒ value 1 is `Other` not `Logic`, and concluded the deleted DLL would have gone to `_otherMissionBehaviors` and never put a null in `MissionLogics`. The RCA's root-cause claim was therefore wrong. Codex also caught a separate v1.4.5 double-tick regression: the `OnTickAsAI → OnTick` rename meant vanilla `Agent.Tick:4768` now auto-ticks attached components, but Claude left the manual `comp.OnTick(dt)` call in `WargMissionBehavior`/`SpiderMissionBehavior` — 2× ticks per frame. Both fixed.).
- Tracing tick-rate vs wall-clock semantics on user-visible timers (Career cooldown review 30 — caught `OnMissionTick` single-bucket accumulator where `if (acc >= 1f) Tick(1f)` drops elapsed time on long frames; a 2.5s frame drained only 1s of cooldown. The bucket pattern was inherited from the prior charge-based code where 1Hz was the natural granularity. When a feature's semantics shift from "periodic batch work" to "wall-clock-precise gate", revisit any `_tickAccumulator` patterns and prefer per-frame `Tick(dt)`).
- Enumerating IEEE-754 special values when validating user-facing float ranges (Career cooldown review 30 — `float.TryParse` admits `NaN`, `Infinity`, `-Infinity`. Range checks like `<= 0` and `> 3600` BOTH evaluate false for NaN, so a NaN cooldown reaches downstream code and `IsOnCooldown => CooldownRemaining > 0f` returns false because NaN comparisons are always false — ability is "always ready", V re-activates indefinitely. Always insert `IsNaN || IsInfinity` (or `IsFinite` on net6+) BEFORE range gates).
- Tracing the FULL call chain when a defensive Prefix targets one method (CustomBattles NRE+diagnostic review 32 — Claude added a Prefix on `CustomBattleSideVM.OnCharacterSelection` to skip when `SelectedItem == null`. The Prefix worked, but vanilla `RefreshValues()` calls `UpdateCharacterVisual()` UNCONDITIONALLY immediately after the SelectedIndex assignment that fired the now-skipped OnCharacterSelection callback. UpdateCharacterVisual derefs `SelectedCharacter.Equipment[(EquipmentIndex)5]` — and SelectedCharacter is exactly what the skipped OnCharacterSelection would have set. NRE moved one method down the call chain. When patching a callback to skip on bad state, check what runs AFTER the callback in the caller — the bad state often persists through the whole sequence, not just the callback).
- Catching reflection-vs-property-setter cases where the property is the correct API surface (CharacterCreation race-filter review 33 — Claude reflected `_raceSelector` field-set + `OnPropertyChangedWithValue(object, string)` method-invoke. The latter resolved to `null` because `OnPropertyChangedWithValue<T>` is a generic method and `AccessTools.Method` lookup by `(typeof(object), typeof(string))` does not match its `(T, string)` open-generic signature. The corresponding public `RaceSelector { set; }` setter was right there and would have fired the notification correctly. Pattern: before reflecting on a private field, search for a public property that wraps it — the property setter will both update the field AND fire change notifications. Only reflect if the property doesn't exist).
- Catching validator-fallback-as-acceptance bugs where a lookup function's "default for invalid input" sneaks past an allow-list comparison (CharacterCreation race-filter review 33 — `RaceManager.GetRaceNameFromId` returns `"human"` as fallback for unknown IDs with a warning log. `SetPlayerRace` accepted that fallback name, checked it against the culture's allow-list, and for cultures that allow `human` preserved the original invalid integer. Pattern: when state from an entity feeds an allow-list comparison, validate the state's validity *before* using it as the comparison key. The fallback is for logging-and-survival, not for security decisions).
- Catching incomplete narrowing of substring-keyword fallback lists (SiegeDismount review 34 — Claude's prior `/deep-review` pass narrowed `SceneSiegeKeywords` from 5 to 3 substrings but left `siege` in. Codex grep across `Main/_Module/ModuleData/*.xml` found 24 vanilla settlement `Location id="center"` entries with scene names like `empire_siege_001`, `khuzait_castle_siege_001` — those scenes can be loaded as non-combat Missions where `IsSiegeBattle=false`, falsely triggering the fallback. Pattern: when narrowing a substring-keyword list, the next step after "remove the obvious false-positive matches" is "remove the keyword fallback entirely if you can; the engine flag is more reliable." A grep across ALL `ModuleData/*.xml` is cheaper than a player having their mount silently disappear during a settlement visit).
- Catching modifier-loss-on-roundtrip via API-overload audit (SiegeDismount review 34 — Claude documented `ItemModifier` loss as a "known limitation" in the feature doc instead of looking for the modifier-preserving API. Codex pointed out that `ItemRoster.AddToCounts(EquipmentElement, int)` exists alongside the bare `ItemRoster.AddToCounts(ItemObject, int)` overload, and the latter internally calls the former with `new EquipmentElement(item)` — dropping the modifier. Pattern: when an adapter touches an inventory or equipment slot that vanilla treats as `EquipmentElement`-shaped (with modifier), audit the API surface for both overloads before settling on the simpler `ItemObject`-shaped one. The "known limitation" framing is a smell — verify the limitation is actually inherent before documenting it).
- Catching user-facing-promise-mismatch from inherited dev code (SiegeDismount review 34 — Codex flagged that `DismountKeepOnMap` mode 1 was a silent no-op despite the MCM hint promising "horse spawns on map, player on foot." The original developer's decompiled module had the same bug; Claude ported it verbatim without challenging whether the user-visible promise matched the implementation. Pattern: when porting a feature with multiple modes, read the user-facing strings (MCM hints, dropdown labels, tooltips) and trace each one to the implementation. If the promise doesn't match the code, either fix the code or fix the promise — never ship the mismatch).
- Re-decompiling property-getter bodies when an agent's paraphrase contradicts another agent's (player startup review 35 — Claude's `taleworlds-researcher` deep-review agent reported BOTH `Hero.BattleEquipment` and `Hero.CivilianEquipment` fall back to `Campaign.Current.DeadBattleEquipment`. Codex re-ran `ilspycmd` and found the truth: civilian falls back to a separate `DeadCivilianEquipment` singleton. Claude's "guard the slot against the dead-equipment singleton" deep-review fix used `DeadBattleEquipment` for both checks, so the civilian guard never tripped — `FillFrom` would have corrupted the shared `DeadCivilianEquipment` for every dead/uninitialized hero in the session. Pattern: when an agent's API-fact summary table looks "symmetric" across two parallel APIs (e.g. battle/civilian, primary/secondary, initial/final), the symmetry is a heuristic worth doubting — the engine often has parallel-but-not-identical fallback paths. Re-run ilspycmd against the actual installed DLL when paraphrase is suspect).
- Pushing back on "may be intentional" hedge from prior agent (player startup review 35 — Claude's `/deep-review` Agent 5 flagged `shaghana` and `abanissa` as missing from `startup_resources_config.xml` but added "may be intentional zero-gold cultures" to soften the finding. They were genuine bugs: both are full independent kingdoms in the Harad region with their own NPC clans and lords. The hedge masked a HIGH-impact gap. Codex correctly treated the hedge as an open question and verified intent by grepping `taom_spkingdoms.xml`. Pattern: when a prior review hedges with "may be / could be / probably", treat that as an open verification task, not a closed dismissal. The hedge cannot replace verification — push back).
- Tracing the FULL player pipeline for new culture entries, not just the entry point (player startup review 35 Phase 3 self-review — when `playerGold` rows were added for `shaghana` and `abanissa`, Claude verified the cultures existed in `cultures.json` but did NOT verify culture-keyed coverage across the 5 narrative menu JSONs. Codex Phase 3 traced the player flow end-to-end: cultures.json → SetSelectedCulture → parents_menu → childhood_menu → ... → finalize. Both cultures had ZERO entries in all 5 menus. A player picking them would render an empty narrative page that crashes vanilla CC on advance. Pattern: when adding a new ID to a multi-stage feature pipeline, enumerate every stage's source-of-truth and verify presence — not just the entry point. The enumerate-from-source-of-truth rule from Class 1 RCA extends through the FULL pipeline a feature touches, not just the registration file. A 30-second grep across the 5 menu JSONs would have caught the dead-end).
- Comment-vs-consumer mismatch in user-editable config docs (player startup review 35 — Codex flagged that the XML header comment in `startup_resources_config.xml` said "influence is granted to NPC lords" but `StartupInfluenceService` actually applies to eligible CLANS, not lords. Wrong audience for future tuners reading the comment to understand the feature. Pattern: doc/comment text near user-editable config files MUST be verified against the consuming code, not paraphrased from memory. Especially for retuning knobs where the user reads the comment to know what to change).

**Codex run-mode caveats:**
- Codex sometimes drifts into adjacent feature work mid-review (CharacterCreation race-filter review 33 — Codex started implementing a separate `Patch29_CCBodyProperties` feature unrelated to the race-filter scope being reviewed). When this happens, preserve any well-tested useful additions but explicitly call out the scope drift in the review log so the codepath remains intentional and documented. Codex's review focus should not be silently expanded.

- Catching missing vanilla safety gates buried in helper methods (MixedFormations review 36 — Codex flagged that Patch30 returned false to skip `Formation.GetOrderPositionOfUnit` without replicating the navmesh availability check that vanilla performs in `GetOrderPositionOfUnitAux`, the helper the Hold-state branch delegates to. Claude's prior /deep-review Agent 5 traced the entry method and concluded "vanilla path is read-only — safe to skip" but did not walk into the helper. Pattern: when a Prefix returns false, decompile EVERY method the entry calls and replicate every safety gate. The entry method's body is just routing; the helpers contain the load-bearing logic).
- Detecting engine multi-threading from `_MT` suffix and `TWSharedMutexReadLock` (MixedFormations review 36 — Codex flagged that the FormationLayoutService's dict + assignment cache mutations on the hot Prefix path could race against `OnMissionTick` writes from the main thread, because vanilla code shows clear multi-threading markers: `Formation.OrderPositionLock`, `IsFormationUnitPositionAvailableMT`, `using TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock)`. Pure-static-read inference produced the right hypothesis without needing runtime instrumentation. Pattern: before patching `Formation`/`Mission`/`Scene`/positioning methods, grep the vanilla type for `_MT` suffix and `TWSharedMutexReadLock` patterns. If present, the patch fires from worker threads — the service must be thread-safe via lock or immutable state).


- Catching direct equipment-slot mutation that bypasses `InventoryLogic.TransferCommand` (EquipPresets review 37 — Codex flagged that the Load path used `equipment[(EquipmentIndex)slotIndex] = new EquipmentElement(item, modifier)` instead of building `TransferCommand`s and submitting via `InventoryLogic.AddTransferCommands`. Direct slot mutation is "lossless for ItemModifier" but bypasses 4 vanilla guarantees: (a) inventory-roster consumption — the item is conjured from MBObjectManager regardless of whether the player has it; (b) displaced-equipment deposit — vanilla `InventoryLogic.TransferItem` auto-generates a reverse `TransferCommand` for the prior occupant; (c) `AfterTransfer` UI refresh — `RefreshValues` re-reads slot VMs but does not fire mount/harness compatibility callbacks; (d) `IsItemEquipmentPossible` slot-fit and family checks. Claude's prior /deep-review (Agent 5 Cross-System Data Flow) confirmed modifier preservation through the chain but did not trace what vanilla does on the same operation, so the architectural-bypass bug went undetected. Pattern: when a feature mutates equipment from a flow that involves the player's inventory, decompile vanilla's equivalent flow (e.g., `SPInventoryVM.EquipEquipment`, `InventoryLogic.TransferItem`) and route through the same APIs. The "lossless setter" framing is not enough — the surrounding flow matters).

- Catching add-only dict semantics in deserialize-then-mutate flows (EditorCacheRebuild review 38 — Codex flagged that incremental rebuild deserialized the full prior `_settlementToSettlementDistanceWithLandRatio` and then Phase 1 RunFiltered called vanilla `SetSettlementToSettlementDistanceWithLandRatio`, which ends in `Dictionary.Add` — `ArgumentException` on every existing key. Claude's prior /deep-review verified positions/CRCs/diff logic but did NOT decompile vanilla's setter to learn it was Add-only rather than Set-or-replace. Same root cause hit Phase 0 (`SetClosestSettlementToFaceIndex` is also `Dictionary.Add`) and Phase 2 (`_fortificationNeighbors.Clear()` is the vanilla precondition that the parallel builders silently drop). Pattern: when a feature deserializes a vanilla cache structure and then mutates it via vanilla's "add" APIs, decompile the setter to find out whether it overwrites or throws on duplicate. The same trace also reveals which subcaches the deserialize replaces — if Phase 0 ran before deserialize, it's wasted work AND a potential dup-key trap if Phase 0 runs again on the deserialized state).

- Catching partial-state fallback-to-vanilla after Prefix mutation (EditorCacheRebuild review 38 — Patch37 caught service exceptions and returned `true` to "fall back to vanilla", but by that point Phase 0 may have already populated `_closestSettlementsToFaceIndices` via the adapter. Vanilla `GenerateCacheData` then reruns `GenerateClosestSettlementToFaceCache` which calls `SetClosestSettlementToFaceIndex` — `Dictionary.Add` — on the already-populated dict → throws. The fallback was actively unsafe. Pattern: once a Prefix has mutated `__instance`, returning `true` is no longer safe unless the Prefix can rollback OR the vanilla path is idempotent-on-partial-state. Default to returning `false` after logging, and let the next button click retry from a fresh instance).

- Catching position-only-vs-face-resolved snapshot semantics for editor-mode safety (EditorCacheRebuild review 38 — `SettlementSnapshotStore.Save` read `s.GatePosition.Face.FaceIndex` for diff comparison. `CampaignVec2.Face` getter dereferences `Campaign.Current.MapSceneWrapper`; in editor mode `Campaign.Current` may be null. Vanilla's editor cache builder never touches `.Face` on `CampaignVec2` — it uses `Scene` directly. Pattern: when snapshotting `CampaignVec2`-typed data in an editor context, prefer `ToVec2()` (pure position read) over `Face.FaceIndex` (Campaign-routed lazy resolve). The face index is derivable from position via the scene if ever needed; storing it is over-eager).

- Catching void-returning verification that lets a "BUILD COMPLETE" popup ship despite logged shortfall (EditorCacheRebuild review 39 — `VerifyOutputRoundTrip` returned `void`, both success and failure branches only logged, then `RunBuild` unconditionally called `NotifyOnMainThread(summary + " Load the next save to use it.")` regardless of verification outcome. Resume-mode also had a blindspot: `result.Phase1.PairsComputed == 0` short-circuited the distance-count comparison, so a structurally valid but logically truncated file passed silently. Pattern: when a "verify" method emits log lines instead of returning a result, the caller is structurally unable to gate user-visible outcomes on it. Always return a `VerificationResult { Ok, Reason }` (or throw on failure) and have the caller branch on `Ok` before showing success UI. Also: in resume/incremental builds where the build-phase count is 0, capture the live `adapter.EnumerateExistingDistances().Count()` immediately before serialization as the expected count, so verification has a real number to compare against).

- Catching multi-step file rename masquerading as atomic write (EditorCacheRebuild review 39 — `WriteOutputAtomically` did `Delete(.prev); Move(final → .prev); Move(.tmp → final)`. Each `File.Move` is atomic in isolation, but the THREE-OPERATION SEQUENCE is not. A process kill between steps 2 and 3 leaves `final` missing entirely — only `.prev` and `.tmp` remain. The diagnostic warns on the NEXT rebuild, but the next game startup happens first. Fix: on Windows/.NET Framework, `File.Replace(tempPath, finalPath, backupPath, ignoreMetadataErrors: true)` calls the Win32 `ReplaceFile` API which is a single atomic filesystem transaction — `final` is never absent. Keep the `File.Move(tempPath, finalPath)` path only for the first-build case where no existing final exists. Pattern: when claiming "atomic write" in a feature doc, the implementation must be a single atomic primitive. Multiple `File.Move` calls are NOT atomic as a sequence, even if each one is).

- Catching dead config knobs in shipped JSON that mislead tuners (EditorCacheRebuild review 39 — `cache_rebuild_config.json` shipped with 8 fields that had no production consumer: `checkpointEvery`, `enablePathReuse`, `enablePersistentPathCache`, `incrementalSpatialRadius`, `enableDebugQualityCheck`, `enableUiOverlay`, `phase1SkipReversePathfind`, `logVerbosity`. The most misleading was `logVerbosity` — it validated successfully but never affected logger output. The fields were carried as reserved scaffolding for dropped/future phases but exposed to the user with no indication. Pattern: when a feature ships a JSON config, every field in the JSON must have at least one production consumer that actually does work in response to it. Fields that are "reserved for future" should stay in the C# class (so the API is stable for tests and future phases) but NOT appear in the shipped JSON file. If you want to retain documentation, use a separate `<feature>-config-reference.md` describing both active and reserved fields with their wiring status).

- Catching test-seam-skipped production-path coverage gap (EditorCacheRebuild review 39 — `RuntimeCacheRebuildServiceTests` used a `TestableRuntimeCacheRebuildService` subclass that no-ops `SpawnBuild`. This is the correct pattern for testing the gate logic (Trigger acquires the Interlocked lock, Trigger rejects when session not ready) without spinning up `Task.Run`. But the SEAM also skipped `RunBuild`, `VerifyOutputRoundTrip`, `WriteOutputAtomically`, and the `finally _runningFlag = 0` cleanup. The exact failure modes that would regress in a refactor — verification failure handling, atomic write rename sequence, background-thread exception cleanup — were covered only by live testing and reasoning. Pattern: when intercepting a virtual method to isolate the surface under test, audit what the seam SKIPS and add separate tests for those paths. Make the skipped methods individually testable (e.g., `internal virtual VerifyOutputRoundTrip(string, int, int, string)` returning a result struct), then write tests that exercise them directly. Don't let "tests exist" satisfy "tests cover the regression-prone paths").

- Catching DEAD CODE behind an optional-parameter call-site mismatch (CrashReport review 38, 2026-05-25 — `HarmonyCorrelationCollector.Collect(IReadOnlyList<StackFrameSnapshot>, IReadOnlyList<StackFrame>? frames = null)` ran its per-stack-frame `Harmony.GetPatchInfo(mb)` block only when the optional `frames` parameter was non-null. The sole production caller in `CrashReportService.ComposeContext` passed only the snapshot list and left `frames=null`. Result: the entire "Harmony patches affecting every frame" feature — advertised in CHANGELOG and feature docs — was DEAD CODE. Every per-frame entry was constructed with an empty `Patches` list. The 5 deep-review agents all passed; the test suite passed; the renderer faithfully rendered empty lists. Codex caught it by tracing the optional-parameter call site. Pattern: when a collector takes an optional parameter that controls a major output field, write an integration test where the parameter is non-null AND a test where it is null and assert the output differs. The fact that the test suite passes proves the suite is incomplete — fix that too. Generalisation for review prompts: extend "DTO Completeness" trace from "is this field populated?" to "are non-empty values actually produced under normal operation?").

- Catching MCM toggle hint text that promises runtime behavior the code only honors at startup (CrashReport review 38, 2026-05-25 — `EnableCrashCapture` MCM hint said "When off, all Harmony Finalizers no-op and AppDomain hook unsubscribes" but the property was read ONCE at `SubModule.OnSubModuleLoad`. A user disabling the toggle at runtime got nothing. SAME shape as `feedback_user_facing_promise_must_match_code.md` (SiegeDismount review 34) AND the Phase 1 deep-review caught a sibling instance in the SAME feature (`SuspendButterLibHandler` was also decorative). Pattern: when authoring an MCM page, the closing pass MUST grep every property name across the feature's source and assert at least one RUNTIME-PATH consumer beyond the startup read. If the toggle is meant to be startup-only, the hint text must say so explicitly. Generalisation for review prompts: deep-review Agent 5's toggle-cross-reference trace must apply to EVERY toggle in the MCM page, not a hand-listed subset).

- Catching static IoC-cache without lifecycle-reset hook (CrashReport review 38, 2026-05-25 — `CrashReportPatchHelper._service` lazily resolved `ICrashReportService` from IoC and cached forever. `SubModule.OnSubModuleUnloaded` called `IoC.Dispose()` but never cleared the static cache. Bannerlord can unload/reload modules in-process; after reload, Harmony Finalizers fired against a disposed `FileLogger` and silently dropped log lines. Pattern: any `private static T _cached` field holding an IoC-resolved reference MUST have a `ResetForUnload()`-style method called from the corresponding lifecycle hook before `IoC.Dispose()`. Existing memory `feedback_lifecycle_state_matrix.md` covers entity state; this extends it to IoC cache state).

- Catching single-shot guard against an IDEMPOTENT operation (CrashReport review 38, 2026-05-25 — `_butterLibSuspended = true` after first successful `TrySuspend()` meant we never re-disabled if the user re-enabled ButterLib at runtime via its own MCM. Codex decompiled ButterLib's `Disable()` and showed it's a trivial state-set + handler-unsubscribe — calling it twice is fine. The flag was a premature optimisation against a cheap idempotent op. Pattern: before adding a "skip if already done" flag, decompile the target to verify the op is actually expensive. Idempotent operations should NOT be guarded against re-entry; the guard creates more failure modes than it prevents).

- Catching error-path that returns a value indistinguishable from success (CrashReport review 38, 2026-05-25 — `CrashBundleWriter.Write` returned the zip path even after a mid-write `catch (Exception)` block. The caller passed that path to the player-facing notifier ("Open bundle folder") with no way to distinguish complete-bundle from broken-bundle. Pattern: when an error path leaves partial state on disk for forensic inspection, rename the partial artifact to a distinct path (e.g., `*.zip.partial`) AND return `null` to the caller so success/failure values are distinguishable).

- Catching off-main-thread AppDomain hooks reading main-thread-only engine state (CrashReport review 38, 2026-05-25 — `AppDomainExceptionHook.OnUnhandled` can fire on TaleWorlds worker threads (`TWParallel.For` for agent ticks). The Mission/Campaign collectors then read live `Mission.Current`, `Mission.Teams`, agent state without thread-safety guards. `InformationManager.ShowInquiry` directly invokes UI subscribers off-thread. Pattern: any hook that can fire on a non-main thread MUST capture the main thread id at subscribe and gate non-thread-safe consumers. The existing `feedback_detect_engine_threading_via_mt_suffix.md` rule should be extended from "patches on Formation/Mission/Scene" to "any hook that can fire from a non-main thread, including AppDomain.UnhandledException").

- Catching cross-file module-load dependency edge gap (v1.4.5 migration review 40 — TAOM had its `TAOM.Dependencies` project source de-tracked from git in commit `0b16cca` (April 2026, gitignore blast extended to `Dependencies/`). When the project was restored to the working tree for the v1.4.5 migration and rebuilt clean against 1.4.5 DLLs, all 5 Claude `/deep-review` agents reviewed the changes but none checked that `Main/_Module/SubModule.xml` actually lists `TAOM.Dependencies` as a `DependedModule`. The Native/SandBoxCore/Sandbox/CustomBattle deps existed but `TAOM.Dependencies` was missing — so in a clean launcher profile, enabling TAOM would NOT auto-enable Dependencies before Native, the pre-Native Harmony/UIExtenderEx setup would be skipped, and the entire purpose of having a separate pre-Native dependency module would be defeated. Codex's `codex review --uncommitted` (default prompt, gpt-5.5 xhigh reasoning) caught this immediately by treating "what does TAOM need to load before it" as a contract check. Pattern: when a feature's runtime correctness depends on another TAOM-owned module being loaded before/alongside it, the `<DependedModule>` + `<DependedModuleMetadata>` edges in BOTH modules' SubModule.xml are part of the feature's contract — verify them as part of any review that touches either module. Add to deep-review Agent 4 (Completeness) prompt: "for any modification to a TAOM module that depends on another TAOM-owned module's runtime state, confirm the dependency edge exists in SubModule.xml of the consumer.").

- Catching `typeof(X)` used as a static-cctor-trigger when it doesn't trigger (v1.4.5 migration review 40 — `Dependencies/SubModule.cs:45` had `_ = typeof(Bannerlord.UIExtenderEx.UIExtender);` in a try-catch that logged "UIExtenderEx patches applied (5 system patches)". The intent was to force the class's static constructor (where `UIConfigPatch.Patch`, `ViewModelPatch.Patch`, etc. are applied) to execute. But `typeof(X)` only loads the Type metadata object — it does NOT execute the class's static constructor. The "patches applied" log line was a lie; UIExtenderEx hooks weren't installed before Native loaded. All 5 Claude agents read the file and saw `typeof()` as a benign type reference; the intent was implicit. Pattern: when a comment or surrounding code suggests a static-init-as-side-effect, verify the mechanism actually triggers the cctor. `typeof(X)` does not. `RuntimeHelpers.RunClassConstructor(typeof(X).TypeHandle)` does. Memory entry: `feedback_typeof_does_not_force_static_init.md`).

- Catching tooling-out-of-scope-by-convention-but-not-by-impact (v1.4.5 migration review 40 — `tools/decompile_to_folder.ps1` enumerates DLLs under `<install>/bin/<binFolder>/` and has a `Modules` category pattern matching SandBox/SandBoxCore/StoryMode/CustomBattle. But those module DLLs live under `<install>/Modules/<X>/bin/<binFolder>/`, NOT under `<install>/bin/<binFolder>/`. The category never matched anything; the `Modules` subfolder in the decompile output stayed empty. We discovered this empirically during the migration (manual workaround run for SandBox/StoryMode) but the script itself was never patched. Pattern: when a migration's tooling under `tools/` is documented in migration docs as authoritative for the migration, treat it as in-scope for review. Compile-clean tooling that produces structurally incomplete output is invisible to per-file C# review).

- Catching Harmony-owner-allowlist derived from namespace assumption rather than vendored-DLL enumeration (Dependencies/Foundation review 41, 2026-05-27 — `PatchShield.TryUnpatchOffendingPatches` had `owner.StartsWith("TAOM")` as the only protected-owner check. Codex decompiled the vendored BUTR/MCM DLLs and listed every `new Harmony("X")` call site: `Bannerlord.ButterLib.SaveSystem`, `Bannerlord.ButterLib.ObjectSystem`, `Bannerlord.ButterLib.MBSubModuleBaseEx`, `MCM.UI.Adapter.MCMv5`, `bannerlord.mcm.ui.optionsgauntletscreenpatch`, etc. None start with "TAOM". A single MissingMethodException in any vendored BUTR patch target would have unpatched the entire BUTR stack via the (now buggy) allowlist. Pattern: when implementing a Harmony owner allowlist (protect, block, dedupe), enumerate every `new Harmony("X")` in the vendored DLLs we ship, NOT just by namespace prefix. Vendored upstream code uses its own conventions and won't match TAOM's. New memory: `feedback_harmony_owner_allowlist_from_vendored_dll_enumeration.md`).

- Catching exception attribution via `ex.TargetSite` when the patch site's `__args` is authoritative (Dependencies/Foundation review 41, 2026-05-27 — `SubModuleConstructionGuard.SwallowFinalizer` patched `Module.AddSubModule(SubModuleInfo, Assembly)` and read `ex.TargetSite?.DeclaringType?.Assembly.GetName().Name` to identify the culprit. But `TargetSite` is the THROW SITE — for a third-party SubModule ctor body that calls `MBObjectManager.GetObject<T>("missing")`, TargetSite is the TaleWorlds API method, not the third-party SubModule. The attribution would mis-blame TaleWorlds, and a TAOM SubModule whose ctor throws via a TaleWorlds API would have its exception silently swallowed (passes the `if (asmName.StartsWith("TAOM"))` rethrow gate as TaleWorlds-attributed). Codex pointed out that `Module.AddSubModule`'s `__args[0] = SubModuleInfo`, `__args[1] = Assembly` — the authoritative source of "whose ctor failed". Pattern: when a Harmony Finalizer needs to attribute a failure to an assembly/type, prefer the patch site's `__args` (with reflective property reads to avoid binding to specific TaleWorlds versions) over `ex.TargetSite`. Generalisation: TargetSite tells you where the exception was THROWN, not where the call chain ORIGINATED. For attribution, you want the call-chain origin, which is usually available as patch-site args).

- Catching broad-StartsWith prefix vs exact-match for own namespace (Dependencies/Foundation review 41, 2026-05-27 — `SaveShield._enginePrefixes` had `"TAOM"` as a StartsWith prefix in the engine-filter list. Independent consumer mods like `TAOM_Online` and `TAOM_Map` would be filtered out as "engine" during stack-walk attribution, even though they're third-party mods from TAOM's perspective. Pattern: when filtering by own-namespace prefix, distinguish exact-equality (`TAOM`, `TAOM.Dependencies`) from sub-namespace prefix (`TAOM.*` with dot). Broad `StartsWith("TAOM")` matches both TAOM-owned AND consumer mods whose names happen to share the prefix. Memory: extend `feedback_substring_keyword_matches_external_data.md` from XML data IDs to assembly-name filters).

- Catching documented-vs-actual semantic mismatch in "current state" lookups (Dependencies/Foundation review 41, 2026-05-27 — `IncompatibleModDetector.ReadCurrentModlist` walked `Modules/` directories and returned ALL installed modules. Doc-comments and log strings said "enabled" / "newly-enabled". Enabling a previously-installed-but-disabled mod would produce zero diff → culprit analysis says "no new mods". The correct API is `TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()`. Pattern: when implementing "current state" lookups, prefer the engine's authoritative API (semantic = what we want) over reconstructing state from disk artifacts (semantic = what's deployed, not what's active). The semantic gap is silent until downstream consumers reason about "enabled". Generalisation: if the implementation derives state from filesystem instead of an in-memory API, the doc-comments must say "installed" or "deployed", never "enabled" / "active".

- Catching counter-naming-vs-control-flow drift (Dependencies/Foundation review 41, 2026-05-27 — `PatchShield.ShouldSwallow` incremented `_swallowedOther` immediately before returning `false` (= rethrow). The "swallowed" counters in `WriteSessionSummary` over-counted. Pattern: when naming counters after the control-flow effect (swallowed = "we ate it"), verify each increment site is on the path that actually achieves that effect. `return true` = swallowed; `return false` = rethrown. Increment counters BEFORE the return in the matching branch, never on the other side).

- Catching non-unique dedupe key for overloaded methods (Dependencies/Foundation review 41, 2026-05-27 — `PatchShield._unpatched` used `<DeclaringType>::<methodName>` as the dedupe key. Bannerlord has many overloaded methods (Mission.SpawnTroop has multiple, SaveManager.Load has 2). The second overload's failure would skip cleanup because the first overload already marked the name. Pattern: when deduping MethodBase keys, use `Module.ModuleVersionId:MetadataToken` for uniqueness across overloads. String-based "type::name" keys are readable but unsafe for overloaded methods).

- DISPUTING `ref Type[] __result` Finalizer-signature legality with Lib.Harmony source citation (Dependencies/Foundation review 41, 2026-05-27 — suspect S4 was framed as "CRITICAL if wrong" because if `ref Type[] __result` weren't a legal Finalizer parameter shape, all `Assembly.GetTypes()` calls would silently return `null` on error instead of partial. Codex decompiled `0Harmony 2.4.2 MethodCreatorTools.EmitCallParameter` and showed: `case InjectionType.Result: if (type.IsByRef && !returnType.IsByRef) type = type.GetElementType(); ...` and `(parameterType.IsByRef && !returnType.IsByRef) ? OpCodes.Ldloca : OpCodes.Ldloc`. By-ref `__result` in Finalizers uses `Ldloca` to load the result local's address. The signature is legal; assignment mutates the wrapper return value. Cited official docs at https://harmony.pardeike.net/articles/patching-finalizer.html. Pattern: when reviewing reflection-driven Harmony patches with unusual parameter shapes, ALWAYS cite the Harmony source — don't speculate. The legality is determinable from source).

**Codex run-mode caveats (continued):**
- `codex review --uncommitted [PROMPT]` is rejected by CLI (the flags are mutually exclusive). When dispatching for an uncommitted-changes adversarial review with a custom prompt, pipe the prompt via stdin WITHOUT `-` as a positional arg: `cat prompt.md | codex review --uncommitted`. The CLI uses its default review prompt anyway when stdin is treated as content alongside `--uncommitted`, but the stdin is added to the context. Even with the default prompt, Codex's review of uncommitted changes is high-signal (v1.4.5 migration review 40: 3 findings from Codex's default prompt, 0 of which were in the 5 Claude agents' deep-review).

This section is updated by Claude after each review cycle. Last updated: 2026-05-27 (Review 41, Dependencies/Foundation defensive infrastructure — Codex caught 6 confirmed bugs (2 HIGH + 2 MED + 2 LOW) and DISPUTED 5 suspects including S4 ref Type[] __result Finalizer legality (would have been CRITICAL if Codex hadn't independently verified via Harmony source). All 6 confirmed findings fixed in same session. Build green, 2,520/2,522 tests pass. Key pattern: when implementing infrastructure-level Harmony shields (allowlist filters, attribution sources, dedupe keys), the right derivation is from vendored-DLL enumeration + decompiled vanilla call sites, NOT from architectural assumptions about namespaces or "what looks plausible.").

### Intentional Patterns (Do NOT flag these)
- `IoC.Resolve<T>()` in Harmony patch classes — approved service locator usage in entry points only
- `IoC.ResolveAll<T>()` for hook dispatch — intentional multi-hook pattern
- `base.Method()` in GameModels accepting sealed params — adapter conversion happens inside the method body before calling the service
- `SubModule.cs` and `IoC.cs` accessing TaleWorlds types directly — these ARE the boundary layer
- GameModel constructors receiving services via `IoC.Resolve<>()` — registration pattern in `SubModule.cs`
- `/investigate` SKILL.md re-declaring `/freeze`'s PreToolUse hook in its own frontmatter — intentional hook reuse so debugging auto-engages scope-lock; copying the inline hook block to other skills must be a deliberate choice, not a casual paste

### When reviewing `.claude/` harness changes (not C# features)
- Check whether claims about Claude Code's load semantics are verified — official docs at https://code.claude.com/docs/en/skills and /docs/en/hooks and /docs/en/memory are authoritative.
- Skill bodies are NOT in the eager startup context; only frontmatter is. An auditor or linter that counts SKILL.md line-count or full-file tokens as startup overhead is wrong.
- Hooks declared in skill frontmatter only fire while that skill is invoked. Writing a hook's state file from a non-hook-bearing context does NOT activate the hook.
- Rules with ANY `paths:` field are conditional. Always-load rules omit `paths:` entirely. `paths: ["**/*"]` is still conditional under the loader.
- `triggers:` is not in the documented Claude Code skill schema — flag any new skill that uses it as a port-from-other-suite drift.

---

## Project Overview

TAOM is a .NET Framework 4.7.2 mod for Bannerlord v1.4.5. It uses Harmony patches, GameModel overrides, and CampaignBehaviors to implement LOTR-themed game mechanics.

**Build:** `./build.ps1` | **Test:** `dotnet test TAOM.Tests` | **Framework:** MSTest + NSubstitute

---

## Architecture

```
HarmonyPatch / GameModel / CampaignBehavior   <-- THIN (<150 lines, no logic)
                    | delegates to
              Service (IXxxService)            <-- ALL business logic here
                    | uses
              Adapter (IXxxAdapter)            <-- wraps sealed TaleWorlds types
                    | wraps
         TaleWorlds Engine (Hero, Agent...)    <-- sealed, never cross boundary
```

**One-liner:** `[HarmonyPatch/GameModel/CampaignBehavior]` -> `IHookInterface` -> `Service` -> `IAdapter` (sealed types)

---

## Critical Rules (NEVER VIOLATE)

| Rule | Details |
|------|---------|
| **TDD Mandatory** | RED -> GREEN -> REFACTOR. Test first, always. |
| **No `#region`** | Use class decomposition (ADR-003) |
| **No `[Obsolete]`** | Migrate all usage in same PR (ADR-004) |
| **No `#if DEBUG`** | Except IoC.cs registration (ADR-005) |
| **Adapter Pattern** | Services use `IHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
| **Thin Entry Points** | <150 lines, delegate to services (ADR-002) |
| **Research First** | Never guess TaleWorlds behavior — decompile first |

---

## Key Paths

| Component | Path |
|-----------|------|
| Mod code | `Main/` (.NET Framework 4.7.2) |
| Mod tests | `TAOM.Tests/` (MSTest + NSubstitute) |
| Features | `Main/Features/` |
| Adapters | `Main/Adapters/` |
| Core | `Main/Core/` |
| XML config | `Main/_Module/ModuleData/` |
| XSLT files | `Main/_Module/ModuleData/*.xslt` |
| TaleWorlds DLLs | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client` |

---

## Non-Negotiable ADR Rules

| Rule | Detail |
|------|--------|
| Entry points <150 lines | ADR-002: delegate immediately to service |
| No sealed types in services | ADR-007: `IHeroAdapter` not `Hero` |
| Constructor injection only | No service locator in services |
| Convert at boundary | Adapt sealed types in the entry point, not deep in services |
| `?.` for computed properties | TaleWorlds getters crash before your null check |

### IoC Lifetimes

| Lifetime | Use For |
|----------|---------|
| `Reuse.Singleton` | Services, engines, caches |
| `Reuse.Transient` | Hooks, stateless helpers |

### Test Coverage Requirements (ADR-008)

| Component | Required | Notes |
|-----------|----------|-------|
| Services | 100% | Must be mockable via constructor injection |
| Engines | 100% | Pure functions — easy to test |
| Hooks | 80%+ | Use `NSubstitute` mocks for adapters |
| Entry Points | Not required | Harmony/GameModel — test via game |

### Feature File Layout

```
Main/Features/MyFeature/
    IMyFeatureService.cs
    MyFeatureService.cs
    MyFeatureIoC.cs          <-- Reuse.Singleton registrations
    Models/
        TaomMyModel.cs       <-- GameModel override (if needed)
    Hooks/
        MyPatch.cs           <-- Harmony patch (if needed)
Main/Adapters/
    IMyTypeAdapter.cs
    MyTypeAdapter.cs
TAOM.Tests/Features/MyFeature/
    MyFeatureServiceTests.cs
```

---

## Adapter Pattern Rules (ADR-007)

### Core Principle
Services NEVER accept sealed TaleWorlds types directly. Always wrap with adapter interfaces.

### Creating New Adapters
1. **Research first** — Decompile the TaleWorlds class before creating the adapter interface
2. **Interface in `Main/Adapters/`** — `I{TypeName}Adapter.cs` with only the properties/methods the feature needs
3. **Implementation in `Main/Adapters/`** — `{TypeName}Adapter.cs` wrapping the sealed type
4. **Recursive wrapping** — If the sealed type exposes other sealed types, wrap those too
5. **Defensive validity** — Check for dead agents, null references in computed properties

### Property Guidelines
- Identify read-only vs read-write properties from decompiled source
- Use null-conditional operators (`?.`) for computed properties accessing nested objects
- Cache expensive property lookups where appropriate

### Testing
- Adapters are thin wrappers — test coverage via service tests that mock the adapter interface
- Use `NSubstitute.Substitute.For<IXxxAdapter>()` in tests

---

## Harmony Patch Rules

### Research First (MANDATORY)
ALWAYS decompile the target method before writing a patch. Verify:
- Exact method signature (parameters, return types, access modifiers)
- Whether the method is virtual, sealed, or static
- Correct namespace and class hierarchy
- Method existence in Bannerlord v1.4.5

### Patch Types
- **Prefix** — Runs before original method. Return `false` to skip original.
- **Postfix** — Runs after original method. Can modify `__result`.
- **Transpiler** — Modifies IL instructions. Most fragile — use sparingly.

### Architecture Requirements
- Patches are **thin entry points** — delegate ALL logic to services via `IHookInterface`
- Entry point files MUST be <150 lines (ADR-002)
- Resolve services from IoC container, never instantiate directly
- Use thread-local state pattern for multi-patch coordination

### Patch Organization
- Place in `Main/Features/{FeatureName}/Hooks/` directory
- Name: `{TargetClass}{TargetMethod}Patch.cs`

### Common Pitfalls
- Collection modification during iteration — use `.ToList()` copy
- Null handling — TaleWorlds often expects `TextObject.Empty` not `null`
- Event timing — verify when events fire vs when state changes
- Static state — avoid unless using thread-local pattern

---

## GameModel Override Rules

TAOM has 31+ GameModel overrides. All follow the same pattern.

### Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;

    public TaomFooModel(IFooService service)
    {
        _service = service;
    }

    public override float SomeCalculation(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        var taomResult = _service.Calculate(adapter);
        return taomResult ?? base.SomeCalculation(param);
    }
}
```

### Rules
1. **Research first** — Always decompile `DefaultXxxModel` before overriding
2. **Inherit from `Default*`** — Never override `GameModel` directly
3. **Call `base.Method()`** — Unless deliberately replacing behavior, fall through for unhandled cases
4. **Thin model class** — Entry point (<150 lines). All logic in Service
5. **Adapter boundary** — Convert sealed params to adapters immediately
6. **JSON/XML config** — Configurable values in `Main/_Module/ModuleData/configs/`, not hardcoded
7. **Register in SubModule.cs** — via `CreateGameModels()` / `OnGameStart()`
8. **Tests** — Service logic fully unit-tested. Model class itself is thin enough to skip

### Existing Overrides (31+ total)

| GameModel | Overrides | Purpose |
|-----------|-----------|---------|
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `MaxCharacterTier => 10` (vanilla 6) |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | Extended tier wages (T0-T10) + culture feats |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `MaxVolunteerTier => 6` (vanilla 4) |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | Culture army influence feats |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | Culture forest/infantry speed feats |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | Culture hearth growth feats |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | Culture veteran militia feats |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | Culture construction speed feats |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | Culture production feats |
| `TaomCaravanModel` | `DefaultCaravanModel` | Umbar caravan cost feat |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | Umbar renown feat |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | Mounted recruit cost feats |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | Party size feats |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | Food consumption feats |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | Settlement loyalty feats |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | Party morale feats |
| `TaomSmithingModel` | `DefaultSmithingModel` | Smithing energy cost feats |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | Tariff income feat |
| `TaomRaidModel` | `DefaultRaidModel` | Raid damage feats |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | Configurable T7-T10 troop power |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | Configurable blunt/cut damage ratio |
| `TaomPartyHealingModel` | `DefaultPartyHealingModel` | Cultural survival bonuses |
| `TaomTournamentModel` | `DefaultTournamentModel` | Per-participant culture armor + prize pools |
| `TaomAgeModel` | `DefaultAgeModel` | Race-appropriate lifespans |
| `TaomPregnancyModel` | `DefaultPregnancyModel` | Race-appropriate pregnancy durations |
| `TaomHeroCreationModel` | `DefaultHeroCreationModel` | Race-aware hero creation defaults |
| `TaomAllianceModel` | `DefaultAllianceModel` | Racial enmity constraints |
| `TaomKingdomDecisionPermissionModel` | `DefaultKingdomDecisionPermissionModel` | Culture/race-based decision rules |
| `TaomDiplomacyModel` | `DefaultDiplomacyModel` | LOTR faction relationships |
| `TaomExecutionRelationModel` | `DefaultExecutionRelationModel` | Culture-specific execution penalties |
| `TaomInformationRestrictionModel` | `DefaultInformationRestrictionModel` | Encyclopedia visibility restrictions |
| `TaomTargetScoreModel` | `DefaultTargetScoreCalculatingModel` | Army targeting: commitment stickiness, faction priority lists, border proximity |

---

## C# Design Patterns

### 1. Hook Pattern (Harmony -> Hook Interface -> Service)

```
HarmonyPatch (thin)
    -> IOnXxx hook interface
        -> XxxHook implementation
            -> IXxxService (business logic)
```

- Harmony patch resolves `IOnXxx` hooks via `IoC.ResolveAll<IOnXxx>()`, iterates, delegates
- Hook implementation builds context, calls service
- Service contains all logic — uses adapters, fully testable

### 2. Strategy Pattern

For per-culture or per-faction variants:

```csharp
public interface ICultureStrategy
{
    string CultureId { get; }
    float Calculate(IContextAdapter context);
}
// One class per culture, registered as a collection
// Service resolves all and dispatches by CultureId
```

### 3. GameModel Override Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;
    public TaomFooModel(IFooService service) => _service = service;

    public override float Calculate(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        return _service.Calculate(adapter) ?? base.Calculate(param);
    }
}
```

### Anti-Patterns (Flag these)
- Business logic in Harmony patches (must delegate to services)
- Sealed TaleWorlds types crossing service boundaries (use adapters)
- Regular null checks on computed TaleWorlds properties (use `?.`)
- Multiple responsibilities in one service (split it)

---

## XSLT Rules

### Authoritative Source
- **SandBoxCore/ModuleData/** is the authoritative reference for vanilla XML structure
- NEVER use SandBox/ModuleData/ — it has different element names the engine ignores
- Example: SandBoxCore uses `<notable_templates>` (engine reads), SandBox uses `<notable_and_wanderer_templates>` (engine ignores)

### Passthrough Requirements (CRITICAL)
- Always pass through ALL vanilla attributes: `<xsl:apply-templates select="@*"/>`
- Always pass through unmodified child elements: `<xsl:apply-templates select="*[not(...)]"/>`
- Never filter out vanilla attributes — critical ones like `is_main_culture`, `can_have_settlement`, `faction_banner_key` will be silently dropped
- Only override the specific attributes/elements you intend to change

### Identity Transform
Every XSLT file must include:
```xml
<xsl:template match="@*|node()">
  <xsl:copy>
    <xsl:apply-templates select="@*|node()"/>
  </xsl:copy>
</xsl:template>
```

### Common Mistakes
- Overly broad `xsl:template match` catching unintended elements
- Hardcoding attribute values that should be passed through from vanilla
- Missing `xsl:output` declaration
- Forgetting to handle child elements when overriding a parent

---

## Testing Rules (TDD Mandatory)

### Workflow: RED -> GREEN -> REFACTOR
1. Write a failing test FIRST (verify RED state)
2. Write minimum production code to pass (GREEN)
3. Refactor while keeping tests green

### Naming Convention
`MethodName_StateUnderTest_ExpectedBehavior`

### Structure: AAA Pattern
```csharp
[TestMethod]
public void MethodName_State_Expected()
{
    // Arrange
    var mock = Substitute.For<IMyAdapter>();

    // Act
    var result = _sut.DoSomething();

    // Assert
    Assert.AreEqual(expected, result);
}
```

### Framework
- **MSTest** — `[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`
- **NSubstitute** — `Substitute.For<T>()`, `.Returns()`, `.Received()`
- **No Moq** — Project uses NSubstitute exclusively

### Test Organization
Mirror source structure: `TAOM.Tests/Features/{FeatureName}/{ServiceName}Tests.cs`

---

## Harmony Patch Categories (Known Intentional Patches)

These are all registered, intentional patches. Do not flag them as unauthorized modifications.

| Category | Feature | Target |
|----------|---------|--------|
| `Patch0_BattleScenes` | Battle scenes (DISABLED) | `Campaign.InitializeScenes` |
| `Patch1_FirstTimeInit` | First-time initialization | Various |
| `Patch2_RefreshTableau` | Banner tableau refresh | Various |
| `Patch3_SetRace` | Race assignment | Various |
| `Patch4_CharacterSpawner` | Character spawning | Various |
| `Patch5_FaceGen` | Face generation | Various |
| `Patch6_BannerEditor` | Banner editor | Various |
| `Patch7_FactionMap` | Faction map | Various |
| `Patch8_SiegeCampGuard` | Siege camp guard | Various |
| `Patch9_RaceFilter` | Race filter | Various |
| `Patch10_WeatherBoundsGuard` | Weather bounds clamping | `DefaultMapWeatherModel` |
| `Patch11_Diplomacy` | Diplomacy system | Various |
| `Patch12_WarOfTheRing` | War of the Ring | Various |
| `Patch14_Execution` | Execution system | Various |
| `Patch15_BannerLayerLimit` | Banner layer limit | Various |
| `Patch16_AtmospherePersistence` | Forced-atmosphere scenes | `Mission.Initialize` |
| `Patch17_TroopWeight` | Troop weight system | `PartyBase`, `TroopRoster` |
| `Patch18_CulturalFeats` | Custom culture feat registration | `Campaign.InitializeDefaultCampaignObjects` |
| `Patch19_CustomBattles` | Custom battle TAOM factions | `CustomBattleData`, `CustomBattleHelper` |
| `Patch20_NarrativeHorseGuard` | Suppress CC narrative horse crashes | `CharacterCreationCampaignBehavior` |
| `Patch21_ShaderPrecompilation` | Loading screen shader progress | `LoadingWindowViewModel` |
| `Patch22_ArmyTargeting` | Border proximity floor | `AiMilitaryBehavior` |

---

## Commit Conventions

50/72 rule. No AI attribution.

Example: `feat: add garrison patrol calculation`

**Optional trailers** (each on its own line after blank line):

| Trailer | When to use |
|---------|------------|
| `Constraint:` | TaleWorlds limitation blocked the ideal solution |
| `Rejected:` | Alternative approach considered and dropped |
| `Not-tested:` | Parts that can't be unit tested |
| `Research:` | What was decompiled to inform this change |
| `Save-compat:` | Save file impact |

---

## TaleWorlds Research — Lookup Order

**ALWAYS check the pre-decompiled source first.** Only fall back to ILSpy MCP for types not found in the decompiled tree.

| Step | Action | When |
|------|--------|------|
| 1. **Read decompiled source** | Read or search files in `E:\Decompiled_Bannerlord\` | Always try first — instant, no tool overhead |
| 2. **ILSpy MCP** | `mcp__ilspy__decompile_type` / `mcp__ilspy__list_types` | Only if type not found in decompiled source |

### Pre-Decompiled Source (`E:\Decompiled_Bannerlord\`)

The entire Bannerlord v1.4.5 codebase is pre-decompiled and organized by category:

| Folder | Contents |
|--------|----------|
| `Campaign/` | `TaleWorlds.CampaignSystem` — GameModels, behaviors, actions (1,556 files) |
| `MountAndBlade/` | `TaleWorlds.MountAndBlade` — missions, agents, game logic (1,977 files) |
| `Modules/` | `SandBox`, `StoryMode` — module behaviors, views, all `Default*Model` classes (1,362 files) |
| `Core/` | `TaleWorlds.Core`, Library, SaveSystem, Localization (666 files) |
| `Engine/` | Engine, InputSystem, ScreenSystem, Navigation (386 files) |
| `UI/` | GauntletUI, PrefabSystem, PSAI (285 files) |
| `Network/` | Diamond, Network, PlayerServices (147 files) |
| `Platform/` | PlatformService, Achievements, ModuleManager (69 files) |
| `Launcher/` | Launcher.Library, Launcher.Steam (40 files) |
| `ThirdParty/` | Newtonsoft.Json, Steamworks.NET, jose-jwt (1,081 files) |

### Quick Lookup Examples

```bash
# Find a class
find "E:/Decompiled_Bannerlord/" -name "DefaultPartyWageModel.cs"

# Search for a method across all decompiled source
grep -r "GetCharacterWage" "E:/Decompiled_Bannerlord/Campaign/"

# Browse a namespace
ls "E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/GameComponents/"
```

### When to Look Up TaleWorlds Source

1. **Harmony patches** — Verify the target method exists with the exact signature (name, params, return type, access modifier)
2. **GameModel overrides** — Verify the base class method you're overriding exists and has the expected signature
3. **Adapter interfaces** — Verify the TaleWorlds properties/methods being wrapped actually exist
4. **Any API call you're uncertain about** — v1.2 to v1.3 renamed/removed several APIs

### ILSpy MCP Fallback

If a type is not in the decompiled source, use the `ilspy` MCP tool:

```
mcp__ilspy__decompile_type(
  assembly: "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\SandBox.dll",
  type: "SandBox.GameComponents.DefaultPartyWageModel"
)
```

**DLL path:** `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

| DLL | Contains |
|-----|----------|
| `TaleWorlds.CampaignSystem.dll` | Campaign, Hero, Clan, Kingdom, Settlement, MobileParty |
| `TaleWorlds.Core.dll` | BasicCharacterObject, ItemObject, Banner, FeatObject, GameModel base classes |
| `TaleWorlds.MountAndBlade.dll` | Agent, Mission, MissionBehavior, FormationClass |
| `SandBox.dll` | All `Default*Model` classes, SandboxAgentApplyDamageModel |
| `SandBox.View.dll` | MobilePartyVisual, MapScreen, view-layer classes |
| `StoryMode.dll` | StoryMode campaign behaviors |

If neither source is available, mark API usages as `UNVERIFIED` rather than guessing.
