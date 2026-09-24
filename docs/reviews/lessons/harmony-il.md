# Lessons — Harmony & IL (Patches, Transpilers, Prefixes, Patch Lifecycle)

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Harmony & IL (Patches, Transpilers, Prefixes, Patch Lifecycle) lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### Blanket-patching infrastructure must cost its per-call overhead against the hottest conceivable target — exclude hot engine layers by default
A Harmony patch that binds `__originalMethod` makes the generated wrapper execute `MethodBase.GetMethodFromHandle` + try/catch on EVERY invocation (~50µs + allocation). Harmless on campaign-tick methods; catastrophic on anything called per-widget/per-frame. PatchShield (TAOM.Dependencies) attached exactly such a finalizer to *every patched method in the process* (`Harmony.GetAllPatchedMethods()`), which included UIExtenderEx's patches on `WidgetFactory.IsCustomType` + `WidgetTemplate.OnRelease` — methods the tournament UI's accumulated template tree calls ~10^6 times at release. Result: a milliseconds-scale teardown became a measured 104-109s frozen exit (#331), invariant +8,276 gen0 GCs per incident.
- **Why missed:** PatchShield was ported (DR3, 2026-05-27) as crash-tolerance infrastructure and reviewed for correctness/security, never for per-call cost × target frequency. TAOM's hot-path rules cover TAOM's own patch targets — nothing asked "what's the hottest method this will WRAP?" for infrastructure that wraps everything, including other mods' patches.
- **Prevent:** any component that patches methods it didn't choose (shields, loggers, profilers) ships with a target-namespace exclusion list covering hot engine layers (`TaleWorlds.GauntletUI`, `TaleWorlds.TwoDimension` at minimum) and documents the per-call wrapper cost. When adding args to a Harmony finalizer/prefix used at scale, know that `__originalMethod` is per-call reflection, not free metadata. Fix: `PatchShield.ExcludedTargetNamespacePrefixes`.
- **Source:** docs/reviews/rca-tournament-exit-hang-2026-07-06.md (round 2), #331.

### When static analysis and reality disagree, sample the live stack — and treat identical GC deltas as a fixed-workload fingerprint
Two multi-agent rounds (22+ agents) "refuted" the true tournament-exit sink with arithmetic built on ASSUMED counts (widgets ~10^3, "scopes small") — while the decisive evidence sat unexploited in the logs: the gen0 delta was byte-identical (+8,276) across hangs with 4 vs 461 vs 745 agents in different towns, proving a deterministic fixed workload. One in-process stack sample (`ExitStallSampler`: background thread, `Thread.Suspend` + reflection-invoked `StackTrace(Thread,bool)` on net472) named the sink in a single repro after three days of modeling.
- **Why missed:** arithmetic refutations read as rigorous; nobody demanded measured counts, and the relocation fix (Patch60) shipped on an assumed cost budget ("release while renderer alive = milliseconds") that the first post-fix repro falsified — the cost moved WITH the relocated call, which itself proves the call is the sink.
- **Prevent:** (a) an adversarial refutation must cite MEASURED counts for any loop it bounds, or verdict at most PLAUSIBLE; (b) a fix that relocates work needs a measured cost budget before shipping; (c) compare GC/counter deltas across incidents early — invariance discriminates fixed-workload from scaling mechanisms and kills whole hypothesis families (crowd size, town) in one glance; (d) for a reproducible main-thread stall, in-process stack sampling is a one-repro root-causer — `ExitStallSampler` stays as standing diagnostics (thresholds +15/+30/+60s, above the healthy ~9.5s residual).
- **Source:** docs/reviews/rca-tournament-exit-hang-2026-07-06.md (round 2), #331.

### Register every Harmony patch in all three places or it's silent dead code
Every TAOM patch class needs ALL THREE or it never engages with no error/warning/log: (1) `[HarmonyPatch(typeof(X), "Method")]`, (2) `[HarmonyPatchCategory("PatchN_FeatureName")]`, and (3) a matching `_harmony.PatchCategory("PatchN_FeatureName")` call in `Main/SubModule.cs`. TAOM uses category-based patching exclusively — `Harmony.PatchAll()` is never called.
- **Why missed:** `Patch39_BanditPartySize` shipped (Bandit Management, 2026-05-27) with the `[HarmonyPatch]` but no `[HarmonyPatchCategory]` → postfix was dead, bandits spawned at vanilla sizes regardless of the MCM curve. All 5 `/deep-review` Claude agents missed it (Standards checks thin-entry/ADR-002, Compatibility verifies the target signature, Data Flow traces XML/config — none grep `SubModule.cs` for the registration). Codex caught it HIGH.
- **Prevent:** Pre-commit grep gate — for every new patch class confirm `grep -l 'HarmonyPatchCategory'` on the file AND `grep "_harmony.PatchCategory(\"PatchN_"` in `Main/SubModule.cs`. Add patch-to-registration tracing to `/deep-review` Agent 5's prompt as a permanent category.
- **Source:** memory/feedback_harmony_patch_category_registration_verification.md (sibling: `feedback_no_aspirational_enum_values.md`)

### Apply a patch's category at a lifecycle point that PRECEDES the earliest render of the screen it protects — "registered" ≠ "applied in time"
TAOM applies categories at two points: an early batch (`OnSubModuleLoad` + pre-menu `OnBeforeInitialModuleScreenSetAsRoot`) and a late batch (`OnGameInitializationFinished`, gated `_gameInitPatchesApplied`). The late batch is correct ONLY for patches protecting in-game / character-creation screens that appear after a campaign starts. A patch protecting a **main-menu / pre-campaign screen** (Save/Load, main menu, launcher) MUST be in the early batch — `OnGameInitializationFinished` fires on campaign init, which is AFTER the cold main menu has already rendered. Verifying the category is *registered* (string match in `SubModule.cs`) is necessary but NOT sufficient; verify the *lifecycle method* it's applied in runs before the patched target can first render.
- **Why missed:** Issue #299 (2026-06-24). The Save/Load hero-preview CTD guard (`Patch55_BasicTableauRaceGuard` on `BasicCharacterTableau.RefreshCharacterTableau`) originally reused `Patch2_RefreshTableau` (the sibling `CharacterTableau` patches' category) to avoid a `SubModule.cs` edit — but that category applies in `OnGameInitializationFinished`, so the prefix wasn't attached when the cold-menu save list renders (before any game-init). `/deep-review` PASSED: the Data Flow agent's init-ordering trace verified the guard *object* was set before the patch applied and even noted `BasicCharacterTableau` renders on the Load Game screen, then mis-placed `OnGameInitializationFinished` as firing before the menu (conflated "after module load" with "after game-init"); the Completeness agent treated "category registered in `SubModule.cs`" as sufficient ("no SubModule edit needed"). Codex caught it CRITICAL by decompiling `Module.SetInitialModuleScreenAsRootScreen` → `OnBeforeInitialModuleScreenSetAsRoot` → push `InitialState`.
- **Prevent:** For any patch whose target type is rendered on a main-menu / pre-campaign screen (decompile to find the instantiator — e.g. `SaveLoadHeroTableauTextureProvider` for `BasicCharacterTableau`), apply its category in `OnSubModuleLoad`/`OnBeforeInitialModuleScreenSetAsRoot`, not `OnGameInitializationFinished`, with a process-static one-shot guard. `/deep-review`'s Harmony-category check now asks not just "is it registered?" but "is the apply lifecycle point earlier than the protected screen's first render?"
- **Source:** docs/reviews/rca-savetableau-2026-06-24.md

### Apply Harmony patches exactly once per process, gated by a static bool
Harmony patch APPLICATION (`_harmony.PatchCategory(...)`, `_harmony.Patch(...)`) is process-global — it rewrites methods that persist for the whole process across games — so it must run exactly once per process. Applying it in a per-game callback (`SubModule.OnGameInitializationFinished`) with no guard re-applies on every 2nd+ game: duplicate prefix/postfix execution, re-chained transpilers, restarted background threads/watchdogs.
- **Why missed:** Issue #288 (2026-06-18). `OnGameInitializationFinished` applied ~26 categories + manual patches + `BattleLoadStallWatchdog.Start()` on every game-init. Invisible because single-campaign sessions only call it once; the re-application is only reachable by something starting >1 game per process (2nd campaign load, 2nd custom battle, or the shader-precompile walk that starts N games back-to-back — which made it deterministic on item 2).
- **Prevent:** Gate the per-game-init patch block with `private static bool _xxxApplied;` set on first entry (the `_missionTimePatchesApplied` pattern in `OnMissionBehaviorInitialize`). `base.OnGameInitializationFinished(game)` stays OUTSIDE the guard. Confirm the guarded body is 100% process-global wiring — `game` parameter unused inside it; genuine per-game registration (`campaignStarter.AddBehavior`/`AddModel`) must live in `OnGameStart`. A test/play path that loads one game per process will not catch this.
- **Source:** memory/feedback_patch_application_is_once_per_process.md, docs/reviews/rca-repatch-crash-2026-06-18.md

### Make IL-mutating transpilers soft-fail on a missing anchor, or gate their category once
A `[HarmonyTranspiler]` that mutates IL and `throw`s when it can't find its anchor instruction is a latent crash that detonates the first time anything re-applies its `PatchCategory`: re-application chains the transpiler twice in one wrapper rebuild, so the 2nd pass runs on already-mutated IL, can't find the (now-removed) anchor, and throws out of `PatchCategory` → `HarmonyException` → crash. Make it soft-fail (`LogTranspilerDegradation(...); return newInstructions.AsEnumerable();` — return the UNMODIFIED IL before any mutation loop), OR gate its category once for behavior-critical transpilers.
- **Why missed:** This shipped TWICE. `RefreshCharacterEntityAuxPatch` (`Late_Transpiler`) was converted throw→soft-fail in Phase 9b #160, but the sweep never reached `DeliverOffSpring_RaceAssert_Patch` (`Patch13_RaceAge`), which crashed the shader-precompile walk entering item 2/9 on 2026-06-18 (#288) when the walk re-applied its category on the 2nd game-init.
- **Prevent:** When you convert ONE throwing transpiler to soft-fail, SWEEP every sibling of the same shape — `grep` `Main/**/Hooks/**` + `Main/**/Patches/**` for `[HarmonyTranspiler]` + `throw`. Mirror `Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs`. Verify idempotent-or-gated, not "it always finds its anchor in practice."
- **Source:** memory/feedback_transpiler_idempotency_or_gated_once.md, docs/reviews/rca-repatch-crash-2026-06-18.md

### Pin a single-occurrence transpiler swap to ordinal + a nearby anchor, and bail (never fall through)
When a transpiler must modify one specific occurrence of a call that appears N times (e.g. `Settlement.get_IsCastle` appears twice in `AiVisitSettlementBehavior.AiHourlyTick` — recruit gate ~line 269 AND reform-score gate ~line 317): (1) pin to ordinal position (FIRST/Nth), (2) additionally require a uniquely-named landmark method within a small window after it, (3) fail-safe by bailing (return the unmodified stream) if the ordinal occurrence lacks the landmark — NEVER fall through to scan later occurrences.
- **Why missed:** "First match WITH anchor" lets a future engine refactor that moves the landmark near the WRONG occurrence silently retarget the swap with no warning. Pinning to ordinal + requiring the anchor makes wrong-target structurally impossible; a refactor degrades to "patch not applied" (vanilla) instead. Caught HIGH (latent) + MED by deep-review Data-Flow + Compatibility agents on CastleRecruitment `CastleAiTranspiler` (2026-05-31).
- **Prevent:** Size the anchor window generously (24+, not 16) — a release-build optimizer can expand `!=` → two `get_X` + `ceq`, and a too-narrow window silently no-ops the patch. Use a stack-shape-preserving swap (mutate the existing `CodeInstruction` opcode+operand in place so labels stay attached; instance `callvirt get_X(T)->bool` → static `call Helper(T)->bool` is stack-identical). Always log a warning on the bail path.
- **Source:** memory/feedback_transpiler_ordinal_plus_anchor_failsafe.md, docs/reviews/rca-castle-recruitment-2026-05-31.md (findings #2, #3)

### Defer patches whose parameter types have Mission/Campaign-dependent cctors
When a Harmony patch's prefix/postfix parameter type is a TaleWorlds struct/class whose static initializer (`.cctor`) reads runtime engine state (`Mission.Current`, `Campaign.Current`, `MBObjectManager.Instance`, etc.), do NOT apply the patch category in `OnSubModuleLoad`/`OnGameInitializationFinished`. Compiling the Harmony detour wrapper fully loads the type, forcing its cctor to run while that state is null. Apply later from `SubModule.OnMissionBehaviorInitialize` behind a one-shot `static bool _missionTimePatchesApplied` guard, in a shared `[HarmonyPatchCategory("Patch_MissionTime_<MethodName>")]`.
- **Why missed:** TAOM 2026-05-07. `Patch31_FormationSetMovementOrder.Postfix(Formation, MovementOrder input)` crashed mod load with NRE inside `MovementOrder..ctor(MovementOrderEnum)`: v1.3.15 `MovementOrder` is a struct whose static fields run `new MovementOrder(MovementOrderEnum.Invalid)` → `new Timer(Mission.Current.CurrentTime, 0.5f)` → null deref (Mission.Current null in OnSubModuleLoad). Stack shows `MonoMod.Compile` + `Harmony.PatchCategory` frames.
- **Prevent:** Before patching, decompile the parameter type with `ilspycmd` and inspect its `.cctor`/static field initializers for any initialize-on-first-game-state read. Sibling patches on the same method that ALSO take the type must share the same deferred category. Do NOT try/catch "pre-warm" the cctor — once it throws, the runtime caches `TypeInitializationException` and the type is permanently broken for the process.
- **Source:** memory/feedback_movementorder_cctor_mission_current.md (plan troubleshoot-this-error-system-nullrefer-bright-dongarra.md, 2026-05-07)

### Inject a Harmony private field with THREE underscores plus the field's literal name
Harmony private-field injection = `___` (exactly three underscores) + the field's LITERAL name. The trap: TaleWorlds fields are leading-underscore (`_match`, `_state`), so the parameter for field `_match` is `___` + `_match` = `____match` (FOUR underscores), not `___match`. Count from the field name: `field`→`___field`; `_field`→`____field`; `m_field`→`___m_field`. An off-by-one is a HARD CRASH at patch-application time, not a silent no-op.
- **Why missed:** Patch46_TournamentDwarfDismount (2026-06-09, issue #277). The deep-review Compatibility agent correctly decompiled the field as `_match` but asserted `___match` was correct ("`__` prefix + `_match`" — the prefix is `___`, not `__`), and the author trusted that confident verdict on the single item the patch hinged on. The 28 green unit tests couldn't catch it — Harmony patches are NOT applied in the MSTest host, so tests exercise service logic, never patch wiring. `PatchShield` doesn't catch it either: its Finalizers guard patch BODIES (runtime), not patch APPLICATION (the exception propagates out of `Harmony.PatchCategory`, unwrapped in `SubModule.cs`).
- **Prevent:** A Harmony patch is "verified" only after it is APPLIED (in-game or a dedicated patch-application smoke test); signature decompile is necessary but not sufficient. The inner `ArgumentException: Parameter name: <X>` shows exactly the stripped name Harmony searched — if `<X>` is missing its leading underscore, add one underscore. When injecting a `_`-prefixed field, write `____name` and comment the count so the next reader doesn't "fix" it back to three.
- **Source:** memory/feedback_harmony_private_field_injection_underscore_count.md, docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md ("POST-SHIP CRASH")

### When a Prefix returns false, decompile the FULL call chain and replicate every safety gate
A Harmony Prefix returning `false` drops EVERY line of vanilla — including the safety gates vanilla calls in helper methods the entry delegates to. Decompile the entry method AND every method it calls; replicate any navmesh validation, bounds/area checks, team/owner/season gates, and null-fallback paths before setting `__result` and returning `false`. If any gate fails, return `true` so vanilla runs its own fallback. (Generalizes to additive GameModel overrides that stack onto a vanilla `ExplainedNumber` — copy the modifier's full enclosing condition + the vanilla culture/entity-resolution precedence, not just the value.)
- **Why missed:** MixedFormations Codex review #36 (2026-05-06). Patch30 returned `false` on `Formation.GetOrderPositionOfUnit`; the Hold branch delegated to `GetOrderPositionOfUnitAux`, which had a navmesh availability gate (`IsFormationUnitPositionAvailable` → fallback `unit.GetWorldPosition()`). Skipping vanilla dropped the gate → units orderable onto cliffs/walls/siege props. `/deep-review` Agent 5 examined only the ENTRY method and concluded "essentially read-only — safe to skip"; Codex went one level into the helper and found the gate. The additive-GameModel variant produced two cultural-feats bugs (#248): Mordor night-speed feat ungated by `!IsCurrentlyAtSea` (granted +10% at sea where there's no penalty to offset), and culture resolved via `party.Owner?.Culture` instead of vanilla's `PartyBaseHelper.HasFeat` precedence (leader→party→owner→settlement), missing ownerless parties.
- **Prevent:** Anti-pattern to ban: "it just delegates to a helper, so it's read-only/safe to skip" — the helper IS the vanilla logic. For additive GameModel overrides, resolve the entity with the SAME precedence helper vanilla uses (`PartyBaseHelper.HasFeat`, `PerkHelper.*`), not an ad-hoc accessor. Sibling-model audit: when fixing a per-model boundary convention in ONE GameModel, grep `Main/Features/**/Models/Taom*Model.cs` and fix all siblings in the same commit — the culture-resolution gap was caught three consecutive reviews running (Codex 43 speed, deep-review size, Codex 44 troop-upgrade + 3 more siblings: `TaomFoodConsumptionModel`, `TaomPartyMoraleModel`, `TaomPartyHealingModel`).
- **Source:** memory/feedback_replicate_vanilla_safety_gates_in_prefix.md, docs/reviews/rca-cultural-feats-terrain-2026-05-28.md, docs/reviews/rca-cultural-feats-3pack-2026-05-31.md

### Re-enter vanilla via a thread-static bypass flag for "use vanilla" options
When a Harmony Prefix returns `false` to replace a vanilla method AND the replacement UI offers a "use the original/vanilla behavior" option, that option MUST re-enter the vanilla method via a `[ThreadStatic]` bypass flag — do not hand-roll an equivalent loop, even when "the filter looks simple." There is no "the filter is simple" carve-out: vanilla always does more than the filter.
- **Why missed:** QuickActions Codex review #36 (2026-05-06). "Sell All (Vanilla)" hand-rolled a per-row `ProcessSellItem` loop mirroring vanilla's filter triplet (`!IsFiltered && !IsLocked && IsTransferable`). Filter-correct, but vanilla `TransferAll` also does capacity-budget enforcement, settlement-mode handling (`TransferAllForSettlement` for low-gold), `RosterElementComparer` sort, full-stack `Amount` (not 1/row), and `ExecuteRemoveZeroCounts` cleanup — all dropped. The menu label promised vanilla parity; players got divergent behavior.
- **Prevent:** Flag must be `[ThreadStatic]` (concurrent missions don't interfere); reset via `try/finally` (an exception inside the vanilla call must not leave it stuck on). The Prefix early-returns `true` when the flag is set. The "use vanilla", "use feature", and disabled-toggle paths should all reach vanilla via the SAME early `return true`. Does NOT apply to pure-replacement Prefixes with no "use vanilla" option offered.
- **Source:** memory/feedback_vanilla_reentry_via_bypass_flag.md (siblings: `feedback_route_via_engine_command_when_ui_active.md`, `feedback_static_delegate_reads_param_state.md`)

### Call private engine methods from hot-path patches via a cached open delegate, never MethodInfo.Invoke
When a Harmony Prefix/Postfix on a hot method (per-tick, per-party-per-hour, per-frame, per-hit) calls a private TaleWorlds method, bind it ONCE in `Initialize()` to an open-instance delegate and call the delegate — never `MethodInfo.Invoke` (which allocates a fresh `object[]` argument array every call and dispatches reflectively). Open-instance delegate type = `Action<TDeclaringType, TArg1, ...>` for `void` (first type param is the instance), `Func<...>` if it returns; `Delegate.CreateDelegate(delegateType, methodInfo)` with no target makes it open.
- **Why missed:** The existing `harmony-patches.md` rule mandates caching the `AccessTools.Method` LOOKUP (the MethodInfo) but is silent on the per-call array + Invoke-vs-delegate — so "I cached the lookup" reads as compliant while the hot-path alloc survives. Caught HIGH by the deep-review Efficiency agent on CastleRecruitment `Patch42_HourlyTickParty_Postfix` (2026-05-31), which `Invoke`'d the private `CheckRecruiting(MobileParty, Settlement)` per-AI-party-per-hour.
- **Prevent:** Wrap `CreateDelegate` in try/catch and null-guard the delegate in the patch body (fail-safe if the signature drifts).
- **Source:** memory/feedback_hotpath_private_method_open_delegate.md, docs/reviews/rca-castle-recruitment-2026-05-31.md (finding #1)

### Treat patches on Formation/Mission/Scene/physics types as multi-threaded — detect via the _MT suffix
Bannerlord names worker-thread-safe helpers with an `_MT` suffix (`CreateNewOrderWorldPositionMT`, `IsFormationUnitPositionAvailableMT`, `GetNavMeshMT`) and guards shared state with `TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock)` / `Formation.OrderPositionLock`. When you patch any method whose vanilla siblings carry these markers, the patch is invoked from worker threads — any TAOM service state it mutates must be lock-protected or immutable. Do NOT assume single-threaded just because Bannerlord is "a game engine"; engines parallelize physics/AI/formation hot paths aggressively.
- **Why missed:** MixedFormations Codex review #36 (2026-05-06). `FormationLayoutService` mutated `Dictionary<object,...>` from `Patch30_FormationGetOrderPositionOfUnit.Prefix`. `Formation.GetOrderPositionOfUnit` itself lacks the `_MT` suffix, but its callers (`CreateNewOrderWorldPositionMT`, `IsFormationUnitPositionAvailableMT`) carry it — the engine wraps the call in its own threading, so Patch30 fires from worker threads and the cache mutations could race against main-thread `OnMissionTick` work.
- **Prevent:** Before patching `Formation`/`Mission`/`Scene`/positioning types, `ilspycmd` the type and grep its body for `_MT` (case-sensitive), `TWSharedMutexReadLock`/`WriteLock`/`PhysicsAndRayCastLock`, and `OrderPositionLock`-style `*Lock { get; private set; }`. Any hit → make the patch + its service thread-safe (`lock (_lock)`, `ConcurrentDictionary`, or precompute on `OnMissionTick` and read an immutable snapshot). Keep critical sections small (read dict, copy value, exit lock, then pure math).
- **Source:** memory/feedback_detect_engine_threading_via_mt_suffix.md (sibling: `feedback_replicate_vanilla_safety_gates_in_prefix.md`), Codex review #36

### Add explicit identity-equality skips for undocumented TaleWorlds invariants
TaleWorlds engine invariants like `team.IsFriendOf(team) == true` (a team is its own friend) are folklore — usually true, not documented, and custom-battle/multi-team/spectator scenarios have produced violations. When you rely on such an invariant for correctness gating (friendly-fire, charge-target selection, alliance permissions), add an explicit identity-equality skip as belt-and-braces: after `if (team.IsFriendOf(myTeam)) continue;` add `if (team == myTeam) continue;`, and after per-formation iteration add `if (ReferenceEquals(formation, ownFormation)) continue;`.
- **Why missed:** Codex adversarial review of SmartCavalryAI (2026-05-06). `Patch31_FormationSetMovementOrder.NearestEnemyFormation` filtered teams via `team.IsFriendOf(own.Team)`; if a custom-battle scenario returned false for `own.Team.IsFriendOf(own.Team)`, the patch could pick a same-team formation and order a self-targeting charge. `/deep-review` verified the friendly check exists; nobody questioned the invariant itself.
- **Prevent:** When reviewing a correctness/security gate, ask "what TaleWorlds invariant am I relying on, and is it documented?" If "presumed true," add the explicit identity check (cost: one reference comparison per iteration). Documented invariants (`Hero.IsAlive`) don't need it.
- **Source:** memory/feedback_taleworlds_invariant_check_explicit.md, docs/reviews/codex-adversarial-smartcavalryai-2026-05-06.md (Main/Features/SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs)

### Audit polling state machines for the sentinel-vs-terminal collision (observation state matrix)
When a patch/behavior holds static state across frames AND drives it from polling external values (engine counts, file sizes, MBObjectManager queries, VM properties), trace the observation state matrix, not just lifecycle: (1) sentinel/uninitialized (`-1`/`null`/`default`), (2) first real observation BEFORE any work (`0`/`false`/empty), (3) in-progress, (4) terminal (often the SAME encoding as state 2). The recurring bug: change-detection comparing `_lastValue == -1` against an observed `0` fires the terminal/completion branch even though the polled subsystem hadn't started.
- **Why missed:** Shipped HIGH 2026-05-04 (`2700f53`→`2ce453f`). `LoadingScreen_ShaderProgress_Patch._lastShaderCount = -1` (sentinel) collided with `GetNumberOfShaderCompilationsInProgress() == 0` on the first frame after a warm-cache load → completion branch fired, killed its own latch, blank loading screen for the whole compile. Both `/deep-review` (5 agents) and the Codex 2026-04-14 review missed it — both walked happy-path examples starting from `count=100`, never enumerated `count=0` as a first-frame state.
- **Prevent:** When writing such a state machine, add a separate `_hasObservedWork` bool set the first time you see a state-3 value; only fire terminal actions when `current == terminal && _hasObservedWork`. Encoded in `.claude/rules/harmony-patches.md` and the `/deep-review` Agent 5 prompt. Companion (different concern): the csharp-architecture "Entity State Matrix" covers WHEN an entity dies; this covers WHAT values a poll returns and in what order.
- **Source:** memory/feedback_observation_state_matrix.md, docs/reviews/rca-shader-precompilation-initial-zero-latch-2026-05-04.md

### Derive a Harmony owner allowlist from enumerated `new Harmony("X")` call sites in vendored DLLs, not namespace prefixes
When a TAOM defensive shield filters Harmony patch owners (allowlist to protect-from-unpatch, blocklist, or dedupe key), derive the filter from enumerated `new Harmony("X")` call sites in every vendored DLL we ship — NOT from architectural assumptions about namespace prefixes. Vendored BUTR/MCM code uses Harmony IDs that don't match TAOM conventions, so a `StartsWith("TAOM")` filter misses every vendored owner.
- **Why missed:** Codex review #42 (Dependencies/Foundation, 2026-05-27) found `PatchShield.TryUnpatchOffendingPatches` only protected `TAOM*` owners — so the first `MissingMethodException` in any ButterLib-patched method would have auto-unpatched ButterLib's entire patch set. Vendored IDs found via decompile include `Bannerlord.ButterLib.SubModuleWrappers2`, `Bannerlord.ButterLib.ExceptionHandler.BEW`, `butterlib.delayedsubmoduleloader.static`, `Bannerlord.ButterLib.SaveSystem`, `Bannerlord.ButterLib.ObjectSystem`, `Bannerlord.ButterLib.MBSubModuleBaseEx`, `MCM.UI.Adapter.MCMv5`, `bannerlord.mcm.ui.optionsgauntletscreenpatch`.
- **Prevent:** List every vendored runtime DLL in `Dependencies/_Module/bin/Win64_Shipping_Client/`; for each non-system DLL run `ilspycmd <dll> | grep -i "new Harmony("`; build the allowlist from that enumeration (StartsWith for a shared stem like `Bannerlord.ButterLib.*`, exact-match for one-offs). Mentally walk: "if a vendored patch on method X throws MissingMethodException, what's the owner string, and does my allowlist include it?"
- **Source:** memory/feedback_harmony_owner_allowlist_from_vendored_dll_enumeration.md (finding S1, HIGH), docs/reviews/rca-dependencies-foundation-2026-05-27.md (Dependencies/Foundation/PatchShield.cs:ProtectedOwnerPrefixes; sibling: `feedback_substring_keyword_matches_external_data.md`)

### Keep a static reflection-swap active for the screen's whole lifetime, not just construction
When a feature uses a reflection field-swap to fool a vanilla VM into building against a non-current entity (e.g. swap `MobileParty._currentSettlement` so `Settlement.CurrentSettlement` falls through to a remote fief, then `new TownManagementVM()`), the swap-construct-restore-immediately pattern is almost always wrong — vanilla VMs read the static "current X" not just at construction but at every user interaction. Audit ALL methods of the parent VM AND ALL child/sub-control VMs for runtime reads of the swapped static; if any reads it, keep the swap active for the entire screen lifetime (Swap in `OnInitialize` → Restore in `OnFinalize`).
- **Why missed:** Codex review #36 (FiefManagement port, 2026-05-06). Claude restored the swap immediately after `new TownManagementVM()`, missing that `TownManagementReserveControlVM.ExecuteConfirm`/`RefreshDailyDefault` and `SettlementGovernorSelectionItemVM.OnGovernorChosen` read `Settlement.CurrentSettlement.Town` at CLICK time (not cached at ctor) → every reserve confirmation would have null-deref'd or operated on the wrong settlement.
- **Prevent:** After auditing the ctor, `grep "Settlement.CurrentSettlement"` (or the swapped static) across the ENTIRE VM-family namespace — parent + every child/sub-control, every method not just ctors. A lifetime-long swap is safe only when the host GameState has `IsMenuState => true` (campaign time stopped, no AI/behavior ticks) AND no async/background work reads the field in that window — document the assumption in the screen's class comment.
- **Source:** memory/feedback_static_singleton_swap_runtime_audit.md, Codex review #36 (FiefManagement port, 2026-05-06)

### A non-vanilla creature mount needs TWO dismount guards (rider death + non-lethal CanDismount hit)
The engine's native mounted-dismount path is broken for non-vanilla creature mounts (spider, elephant) and is reached on TWO triggers — guarding only one leaves the other a live CTD. (1) Rider death while seated → `Agent.Die` AVs → Patch47 prefix on `Agent.Die` hard-dismounts via the private `SetMountAgent(null)` so the rider dies the proven on-foot death. (2) A non-lethal `CanDismount` melee hit on a SURVIVING mounted rider → `Agent.HandleBlowAux` AVs reading `0x3` → Patch48 prefix on `Agent.HandleBlowAux` strips `BlowFlags.CanDismount` when the victim's mount is the creature Monster (native dismount never fires, rider stays on the locked mount, damage still applies).
- **Why missed:** Patch47 (death) alone is insufficient — it only hard-dismounts before `Die`; a non-lethal dismountable hit still reaches the broken native path. Both guards are spider-only today; the elephant mahout shares the identical architecture and has the latent hit-fault (unsurfaced only because mahouts are rarely melee-reached). The rider's own animations are NOT the cause (`as_goblin_warrior` inherits the full human death/fall surface via `base_set="as_human_warrior"`).
- **Prevent:** Any future ridden creature mount needs BOTH guards — add it to the `docs/ai-includes/creature-mount-authoring.md` recipe. Process lesson: don't over-fit a TRUNCATED native stack to a hypothesis — the first hit-crash report (only `TickMissionAux → Mission.Tick` + "bite flood before crash") was misdiagnosed as NaN geometry; the full frame chain + debugger Blow/victim state (finite blow, mounted rider, `CanDismount` flag) named the real path immediately.
- **Source:** memory/feedback_creature_mount_dismount_guards_death_and_hit.md, docs/reviews/rca-spider-dismount-on-hit-2026-06-15.md

### Tournament mount comes from the culture weapon template, not GetParticipantArmor — postfix PrepareForMatch to dismount
A tournament participant's `Equipment` is assembled by TWO methods on `TournamentFightMissionController`, and the mount is owned by the one NOT named for armor. `PrepareForMatch()` clones the culture tournament WEAPON template (`CultureObject.TournamentTeamTemplatesFor{One,Two,Four}Participant` / `tournament_template_empire_*_participant_set_v1` fallback) into each `participant.MatchEquipment` — carrying weapons (slots 0–4) AND a horse (slot 10 `EquipmentIndex.Horse`) + HorseHarness (11). `AddRandomClothes()` calls `TournamentModel.GetParticipantArmor` and copies only armor slots 5–9. So a `GetParticipantArmor` override (and the `gear_practice_dummy_<culture>` NPCs it resolves) can NEVER add/remove a horse.
- **Why missed:** Dwarves (custom-skeleton race) spawn inside the horse mesh (misaligned rider bone, same defect as the `EyeHeightAdjustmentHook` fix). The bug looked like it should live in "ParticipantArmor", but that override is provably slot-5–9 only.
- **Prevent:** Fix = Patch46_TournamentDwarfDismount (2026-06-09, issue #277): postfix the public `PrepareForMatch`, inject the private field via `TournamentMatch ____match` (FOUR underscores), iterate `____match.Teams → team.Participants`, and for any participant whose race must fight on foot clear `EquipmentIndex.Horse` + `HorseHarness` via `AddEquipmentToSlotWithoutAgent(slot, EquipmentElement.Invalid)` (`Invalid.Item == null`; `Mission.SpawnAgent` guards mount creation on `Item != null`). `PrepareForMatch` is the single chokepoint feeding both the visual spawn (`SpawnAgentWithRandomItems`) and AI `Simulate` (`GetSimulationAttackPower`). Key on RACE not culture (`ITournamentService.ShouldDismountInTournament(int raceId)`, validate-before-lookup via `IRaceManager.IsValidRaceId`→`GetRaceNameFromId`→`"dwarf"`). The town arena practice fight is already horse-free by vanilla `.NoHorses(true)`. Generalizes: when fixing a bug about an ASSEMBLED value (equipment/stats/visuals built by several methods), enumerate every producer and confirm which slot/field each owns — don't assume the concept-named method sets the offending field.
- **Source:** memory/feedback_tournament_horse_from_weapon_template_not_armor.md, docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md, docs/features/arena.md

### A fail-safe that falls back to a path re-executing the SAME failed operation contains nothing
When a guard catches an exception and "fail-safes to vanilla behavior", trace what the vanilla path actually does next — if vanilla later re-executes the operation that just failed (a deferred teardown, a retry loop, a second walker over the same structure), the catch only relocated the crash from a guarded call site to an unguarded one. The fail-safe must either guard the vanilla re-execution too, or prevent it from re-touching the poisoned state.
- **Why missed:** Issue #339 (2026-07-13, v2.0.12 player CTD). Patch60's tournament-exit release caught an `AccessViolationException` mid-`WidgetTemplate.OnRelease` walk and fell back to "today's vanilla leak" — but the vanilla leak means `GauntletLayer.ClearContext` re-walks the SAME corrupt template tree at `ScreenManager.PopScreen`, where the identical AV escaped uncaught and killed the session. The round-1/round-2 reviews all accepted "worst case = vanilla behavior" without asking what vanilla does with the state the failure leaves behind.
- **Prevent:** For every `catch → fall back to vanilla` in a patch, answer in the code comment: "does the vanilla path re-execute this operation on this same state later, and is THAT site guarded?" Fix here = Patch62_MovieReleaseAvGuard, an AV-only Finalizer on `GauntletMovie.Release` itself — the shared chokepoint both attempts flow through — so the first suppression also removes the movie from `_movieIdentifiers` and the re-walk never happens.
- **Source:** issue #339, crash signature 4698b4d4 (player report 2026-07-13), docs/reference/harmony-patch-registry.md § Patch62_MovieReleaseAvGuard
### A position/layout Prefix that returns `false` for EVERY unit silently overrides other features' unit placement
MixedFormations' `Patch30_FormationGetOrderPositionOfUnit` Prefixes `Formation.GetOrderPositionOfUnit` and returns `false` (suppressing vanilla) for every unit in an open-field battle, substituting its own computed position. When BannerBearers (2026-07-16) started giving formations banner bearers, the engine placed them via `SwitchUnitLocations` into its dedicated `RelativeFormationPosition[6]` banner slots -- and Patch30 then overrode where they actually stood, scattering the standards through the ranks. No crash; the bearers still carry banners and still grant the formation effect. Fixed by letting bearers fall through to vanilla: `if (unit?.Banner != null) return true;`, placed before the IoC resolve to keep the ~40,000x/frame path cheap (`Agent.Banner` is `Equipment?.GetBanner()` -- one `_weaponSlots[4]` read, no loop, no allocation).
- **Why missed:** this was a KNOWN UNKNOWN, not an unknown unknown -- the feature doc, the plan, and the deep-review Data Flow agent's brief all named "MixedFormations + banners = possible arrangement thrash" as the top untested interaction. It still went unresolved, because resolving it required reading a DIFFERENT feature's patch and reasoning about which one wins. Per-feature review scopes structurally exclude that. Codex, given the whole repo and no scope boundary, found it immediately.
- **Prevent:** when a feature starts producing a new KIND of unit/entity the engine positions specially (banner bearers, detached units, siege-engine crew), grep every TAOM Prefix on the relevant engine positioning method and check each for a blanket `return false`. A blanket-suppress Prefix is a silent monopoly on that decision -- every future feature that relies on the vanilla path breaks against it with no error. Conversely, when WRITING such a Prefix, prefer falling through (`return true`) for any unit the engine has special plans for. Flagging an interaction as "untested" in a doc is not the same as resolving it: schedule the cross-feature trace, or hand it to a whole-repo reviewer.
- **Source:** docs/reviews/rca-banner-bearers-2026-07-16.md (Codex C2, MED).

### The master-toggle fold applies to ANY engine-replacing patch, not just GameModel overrides — and a policy method that folds `!Enabled` into a denial is wrong for callers needing vanilla parity
Patch63's first cut gated reinforcement bearers on `IsFormationGroupAllowed`, which folds `!Enabled` into `false`. With the feature OFF, the prefix (which replaces the engine method unconditionally) therefore declined every bearer — permanently starving vanilla hero-captain formations of mid-battle replacement bearers, strictly worse than vanilla. Same regression class the 2026-07-16 review caught on the model layer (`return base(...)` when disabled), reintroduced through a Harmony prefix instead of a GameModel.
- **Why missed:** the toggle-fold lesson was scoped to GameModel overrides ("every override's disabled path must be `return base`"); a prefix-replacement patch is the same shape — TAOM code standing where engine code stood, still consulted when the feature is off — but no rule or agent prompt said so. The author reused an existing service gate without tracing its disabled-state branch.
- **Prevent:** any patch that REPLACES an engine method must answer "what does this do when the feature is disabled?" with "exactly what the engine did" — crash guards may stay, policy must not. Fold the toggle at a purpose-built decision method whose disabled branch encodes the caller's parity need (`IsReinforcementBearerAllowed`: disabled ⇒ allowed), never by reusing a policy getter whose `!Enabled` branch means "deny". Deep-review Agent 5's master-toggle-fold check now has a worked prefix-form example.
- **Source:** docs/reviews/rca-banner-bearers-reinforcement-av-2026-07-25.md (Flow-4, HIGH, fixed in-session).

### When two TAOM components can attach finalizers to the same method, one must yield

Harmony runs every finalizer on a method against ONE shared exception slot; each non-void return
overwrites it and the wrapper ends `if (ex is not null) throw ex`. So two TAOM-owned finalizers on
one method do not compose — the last one to run decides, and which one that is depends on Harmony's
ordering, not on either author's intent.
- **Why missed:** `PatchShield` (blanket, shields every patched method in the AppDomain) and
  `SaveShield` (10 named save/mission methods) had always overlapped harmlessly, because both
  unconditionally swallowed. The 2026-07-31 co-op work made SaveShield's return *conditional*
  (rethrow on the SAVE-LOAD category during a co-op session) — at which point PatchShield's
  unconditional swallow of the missing-API trinity silently overrode it on exactly those methods, and
  a partially deserialised campaign would load with no error. Both shields were reviewed as units;
  nothing asked what happens where they meet. TAOM now owns five distinct Harmony ids, so this is a
  class rather than an incident.
- **Prevent:** when adding or changing a finalizer, enumerate the other TAOM-owned Harmony ids and
  check target-set overlap. Where two overlap, the broader one skips (`PatchShield.Install` now
  consults `SaveShield.IsShielding`). Any change that makes a finalizer's return value conditional
  must re-check every co-located finalizer on the same targets — an unconditional neighbour defeats a
  conditional one every time.
- **Source:** `docs/reviews/rca-coop-interop-2026-07-31.md` finding #2 (found by the completeness
  critic, not by any of the 7 per-component review dimensions)

### Read every patch collection Harmony exposes, not the four you remember

Lib.Harmony 2.4.2's `Patches` has SIX `ReadOnlyCollection<Patch>` fields: `Prefixes`, `Postfixes`,
`Transpilers`, `Finalizers`, **`InnerPrefixes`, `InnerPostfixes`**.
- **Why missed:** the Harmony census was modelled on `HarmonyCorrelationCollector`, which reads four,
  and inherited the assumption. An owner whose only patch on a method is an inner prefix is then
  absent from the census entirely — and a missing owner is precisely the signal that report tells the
  reader to interpret as "two 0Harmony instances are loaded", sending them after a load-order problem
  that does not exist.
- **Prevent:** when enumerating Harmony patch info, decompile `HarmonyLib.Patches` against the pinned
  Lib.Harmony version rather than copying an existing call site. `HarmonyCorrelationCollector` still
  has this gap — under-reporting inner-patch owners in crash reports.
- **Source:** `docs/reviews/rca-coop-interop-2026-07-31.md` finding #9

### A sequence of unguarded `PatchCategory` calls fails as a group, and the log cannot tell you it did

- **Symptom:** `Main/SubModule.cs` applied the seven categories owning the entire character-preview
  path (`Patch1_FirstTimeInit` … `Late_ActionSetOverride`) as consecutive bare statements. Any one
  throwing would silently prevent every later one from applying, leaving the preview on vanilla
  resolution — a state that produces a prone/bind-pose character and is **indistinguishable, in every
  log we ship, from all seven applying correctly**.
- **Why missed:** nothing asserted the outcome. There was no success log, no failure log, and no
  binding test over the reflection sites those patches depend on, so "patches applied" was an
  assumption held for the life of the feature. Four `catch` blocks inside the same patches also
  swallowed exceptions with no trace, so a reflection failure against a drifted engine looked exactly
  like the patch never running.
- **Prevent:** when a batch of patch categories backs one user-visible feature, isolate each in its
  own try/catch and log the outcome per category — a failure must name itself. Treat any `catch` in a
  patch that exists "so the game keeps booting" as requiring a log line: silence there converts a
  diagnosable fault into an invisible one, on the machines you cannot reach.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md`

### A blanket "shield everything patched" mechanism costs what OTHER mods patch, not what you patch

PatchShield attaches a finalizer to every method Harmony has patched. That finalizer binds
`__originalMethod`, so Harmony's generated wrapper pays a `MethodBase.GetMethodFromHandle` plus a
try/catch **per call** (~50 µs). The population it wraps is therefore chosen by whatever else is
installed — the cost scales with the modlist, not with the mod that owns the shield.

- **Why missed:** #331 fixed this once, for the case that had bitten (the Gauntlet UI layer), with a
  hand-maintained `ExcludedTargetNamespacePrefixes` denylist. That framing treats the tax as a
  property of specific hot namespaces. It is not — it is a property of *how many methods anyone has
  patched*. BannerlordCoop's AutoSync transpiles every declared method and constructor of 43 campaign
  types, so installing TAOM alongside it multiplied the shielded population enormously without TAOM
  changing a line, and collapsed frame rate on the campaign hot path. A denylist cannot anticipate
  the next mod. TAOM's own deep review had raised the question and could not measure it; a player
  with a profiler answered it.
- **Prevent:** for any mechanism that instruments a set it does not control, gate on the SIZE or
  ORIGIN of that set, not on a curated list of members. Here: skip installation entirely when a mod
  known to patch broadly is present (`PatchShieldPolicy.ShouldInstall`). When adding such a
  mechanism, ask "who decides how many methods this wraps?" — if the answer is "other mods", it
  needs a budget or a presence-keyed opt-out from day one.
- **Source:** `docs/reviews/rca-tournament-exit-hang-2026-07-06.md` (round 2, the original) +
  player report 2026-08-02 (the recurrence under BannerlordCoop);
  `docs/reviews/rca-coop-authority-gating-2026-08-01.md` open question #2

### A mission's behavior list is not its `InitializeMissionBehaviorsDelegate`

`OpenTournamentFightMission` returns 13 behaviors. The live mission runs **65** — `MissionView`s are
registered by the view system, separately, and never appear in that delegate. Reasoning about what a
mission can and cannot do from the initializer alone will be wrong by a factor of five.

- **Why missed:** a player CTD logged `agent#0 'Musician' char='musician_dunland'` in a
  `TournamentFight`. Three independent analyses — two subagents and the orchestrator — each read the
  13-behavior delegate, confirmed it has no `MissionAgentHandler`, separately confirmed that
  `FightTournamentGame.GetParticipantCharacters` cannot select a musician, and concluded the agent
  had no code path. Every one of those facts is true. The agent is an arena **spectator**:
  `MissionAudienceHandler` (`SandBox.View`) draws the crowd from the settlement culture's location
  characters with `Culture.Musician` at weight 0.1. Three analyses agreeing did not make the
  conclusion less wrong — they shared one unexamined premise, which is what agreement between agents
  reasoning from the same starting document buys you.
  `MissionDiagnosticBehavior` had already dumped all 65 behaviors into **the same log file** being
  analysed.
- **Prevent:** when a mission-scoped question turns on "what is in this mission", read the live dump
  (`[MissionDiag] === Mission start: … behaviors=N ===`) before the engine source. If no dump exists,
  say the list is unknown rather than substituting the initializer for it. Corollary for
  orchestration: when parallel agents converge on a conclusion, check whether they were handed the
  same premise — convergence is only evidence when the paths were independent.
- **Source:** `docs/reviews/investigation-dunland-tournament-ctd-2026-08-02.md`

### Guard a vanilla crash by REPAIRING ITS PRECONDITION, not by reimplementing the method

`HeroSpawnCampaignBehavior.SpawnLordParty` throws `InvalidOperationException` at an unguarded
`Settlement.All.First(x => x.Culture == hero.Culture)` — reached only when the hero's faction has no
`InitialHomeSettlement`. The obvious guard is a prefix returning `false` that computes the spawn
settlement itself, and it would have had to carry the spawn position, the `isNewGame` roster fill and
`GiveInitialItemsToParty` forward by hand, then track them across every engine bump.
Patch65_LandlessCultureSpawnGuard's prefix instead gives the faction an `InitialHomeSettlement` so
vanilla takes the branch **above** the throwing line: every downstream side effect stays vanilla and
the patch owns exactly one property write. The repair also persists — `Clan.InitialHomeSettlement` is
`[SaveableProperty(114)]`, so it is one write per broken faction on existing saves, not a per-tick
patch-up. (`Clan.SetInitialHomeSettlement` is public; `Kingdom.InitialHomeSettlement` has a private
setter reached through a statically-cached `AccessTools.PropertySetter`, never per call.)
- **Why missed:** "vanilla throws on line N" routes by reflex to skip-original — the most expensive
  and most drift-prone shape, and the one whose full cost the sibling entry "When a Prefix returns
  false, decompile the FULL call chain and replicate every safety gate" exists to police. The cheaper
  question is which *input* put vanilla on the throwing branch, and whether a patch can supply it.
- **Prevent:** before writing a skip-original prefix, read the lines ABOVE the throw for a branch
  vanilla would rather have taken and ask what state would make it take it. Order the anchor search
  deterministically with no RNG (`hero.HomeSettlement` → `hero.BornSettlement` → clan leader's
  settlement → nearest non-hostile → nearest of any allegiance, lazily evaluated), and order the
  guard's gates by cost with a test pinning the order — `FactionHasInitialHomeSettlement` is one
  property read and clears every healthy faction, while the culture check walks all 988 settlements.
- **Source:** #374 + [lord-spawn-guard.md](../../features/lord-spawn-guard.md) +
  `docs/reference/harmony-patch-registry.md` § Patch65_LandlessCultureSpawnGuard

### Scope a backstop finalizer to the exception it was written for, and make it say what it suppressed

A finalizer that swallows broadly and silently converts a crash into an invisible bug — the game
keeps running and the defect now reports as "a lord never showed up", six months later, on a machine
you cannot reach. Patch65's finalizer sits under the prefix as a backstop and does three things
deliberately: it catches `InvalidOperationException` ONLY (everything else propagates untouched), it
nulls `__result` only because `ConsiderSpawningLordParties` already null-checks before
`GiveInitialItemsToParty` so the lord simply raises no party that day, and it names the hero it
suppressed for once per hero rather than swallowing quietly.
- **Why missed:** the swallow is written to keep the game booting and the log line feels like noise at
  the moment you type it, so it gets dropped — the same failure the sibling entry "A sequence of
  unguarded `PatchCategory` calls fails as a group" documents from the other direction. The narrowing
  is skipped for the same reason: a bare `catch` is shorter, and it looks identical to a scoped one
  right up until it eats an unrelated fault.
- **Prevent:** three questions before shipping any backstop finalizer — which exception TYPE is this
  written for (catch that, rethrow the rest); what does the caller do with the neutralized result
  (read the caller, and record the answer in the code comment); and what does the log say when it
  fires (name the subject, once per subject, not per tick).
- **Source:** #374 + [lord-spawn-guard.md](../../features/lord-spawn-guard.md)

### Injected-field parameters need FOUR underscores, and a field-resolution binding test cannot catch it

Harmony strips **exactly three** underscores from an injected parameter name and looks the remainder
up as a field. Engine fields carry a leading underscore, so binding `_race` requires `____race`.
Writing `___race` asks for a field literally named `race`, and Harmony throws
`ArgumentException("No such field defined in class …")` while applying the **whole category** — every
patch in it, not just the offending method.

- **The trap is that the obvious test does not cover it.** Per-patch binding tests assert
  `AccessTools.Field(type, "_race") != null`. That resolves identically whether the parameter is
  spelled with three underscores or four, so it exercises the *field name* and never the *parameter
  naming convention*. Patch67 shipped with three underscores, passed its own four binding tests and
  the full 5588-test suite, and failed only at runtime.
- **It fails silently in-game.** TAOM's isolated preview/patch batches catch a category failure and
  log it rather than crash, so the symptom is a patch that simply never runs. It was found only by
  reading the live `taom_debug_*.log` after an in-game repro — a green build proves nothing here.
- **Gate:** `TAOM.Tests/Migration/HarmonyFieldInjectionNamingTests.cs` now scans every
  `[HarmonyPatch]` class in the TAOM assembly, strips exactly three underscores from any `___`-prefixed
  parameter, and asserts the remainder resolves on the patch target — suggesting the four-underscore
  form when `_<name>` exists. It carries a non-zero-coverage assertion so a broken scan cannot go
  vacuously green. Verified red-then-green by reintroducing the defect.
- **Source:** #389 (Patch67 render census), 2026-08-06.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/return-to-army.md](../../features/return-to-army.md)
- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)
- [docs/reviews/lessons/testing-qa.md](./testing-qa.md)
- [docs/reviews/rca-castle-recruitment-2026-05-31.md](../rca-castle-recruitment-2026-05-31.md)
- [docs/reviews/rca-isengard-black-tableau-2026-08-06.md](../rca-isengard-black-tableau-2026-08-06.md)
- [docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md](../rca-tournament-dwarf-dismount-2026-06-09.md)

<!-- backlinks-end -->
### A containment finalizer must swallow only the exception class it exists for

**Symptom:** `Patch69_TournamentEndGuard` was written to contain a known `NullReferenceException` in
vanilla `TournamentVM.OnTournamentEnd`, but returned `null` (swallow) for *every* exception class once
logging succeeded.

**Why missed:** the patch was framed as "contain the crash" rather than "contain THIS crash", so the
question "what else can this method throw?" was never asked — even though the same triage had already
established that `OnTournamentEnd` opens with `Round4.Matches.Last(m => m.IsValid)`, and
`Last(predicate)` throws **InvalidOperationException**, not NRE, when nothing qualifies. A real
bracket-construction bug would have become a silently half-drawn screen with no crash bundle: strictly
worse than the crash, and invisible.

**Prevent:** enumerate the target method's throwable classes before writing a finalizer, then gate on
the specific one(s) and `return __exception` for the rest so they still reach the crash reporter.
TAOM's own `PatchShield.ShouldSwallow` is the reference implementation — it eats only
MissingMethod/MissingField/TypeLoad and rethrows everything else, deliberately.

**Source:** `docs/reviews/rca-patch69-tournament-guard-2026-08-07.md` finding 8 (Codex P3, #407).

### Before documenting when a patch runs, grep every call site of the patched method

**Symptom:** `Patch50_DropFlaggedItemGuard`'s doc-comment and registry entry both described
`Agent.CheckToDropFlaggedItem` as reached via `Mission.OnAgentHit`. The engine calls it from
**three** sites — `Agent.OnMount` (:12142), `Agent.OnDismount` (:12167) and `Mission.OnAgentHit`
(:57869), v1.4.8. A Prefix documented as "runs per creature bite" actually runs on every mount,
every dismount and every agent-hit in every battle. The efficiency review then costed it against
the wrong frequency.

**Why missed:** the patch was authored from a crash stack, which is read *backwards* from the throw.
That naturally answers "how did I get here" and never asks "who else arrives here". A patch target
is a fan-in point; a stack trace shows exactly one of its inbound edges.

**Prevent:** before writing any sentence about when a patch runs, grep the decompiled dump for the
target method name and enumerate every caller — `grep -n "MethodName" <dump>` takes seconds. Put the
full call-site list in the doc-comment, because the next reader will otherwise inherit the single
edge the original stack happened to show. This matters most for guards: the frequency claim drives
whether allocation, logging or reflection in the patch body is acceptable.

**Corollary — the same evidence bar applies to prose as to code.** In the same review, three further
findings were all unverified sentences about engine behavior ("mid-teardown", "both branches end in
no item dropped", an overstated Finalizer reach) written in a session where every *code* claim had
been checked against the installed decompile. `evidence-over-claims.md` §C names tool output, counts
and signatures — the artifacts of doing work — but not doc-comments and design rationale. Inaccurate
patch documentation is not cosmetic here: this very bug took eight weeks to diagnose because
Patch50's original comment confidently asserted the victim was a mount, and the live debugger later
showed it was not.

**Source:** `docs/reviews/rca-dropflaggeditem-guard-2026-08-10.md` findings 1–4.

### "Fall through to vanilla on error" is only safe when vanilla is a safe default at THAT call site
TAOM guard prefixes conventionally `return true` from their catch, handing control back to the engine. That is right almost everywhere, because vanilla is a working default and the guard is an enhancement. It inverts when the guarded method is itself the crash: `Patch71`'s catch returned `true`, so any internal fault re-raised the very NRE the patch existed to stop, on a hero `RemoveCompanionAction.ApplyInternal` had ALREADY de-clanned, de-partied and made a fugitive. The error path recreated the torn campaign state the fix was written for.
- **Why missed:** the shape was copied from `Patch8_SiegeCampGuard` and `Late_ActionSetOverride`, where deferring genuinely is the safe outcome, without re-deriving whether vanilla was safe here. "Falls through to vanilla" describes control flow, not an outcome, so it reads as safe wherever it appears.
- **Prevent:** before writing a defer-on-error catch, state in one sentence what vanilla actually DOES at this call site. If the answer is "throws", or "throws after mutating shared state", the catch must skip the original instead, and the patch's registry entry must record the departure so nobody restores the convention. A prefix that cannot throw also removes the need for a Finalizer, which could only suppress a torn write, never repair it.
- **Source:** docs/reviews/rca-field-commission-reset-equipments-2026-08-20.md, #486.

### A patch's own try/catch cannot survive a JIT-time member-resolution failure in its own body
`MissingMethodException`/`MissingFieldException`/`TypeLoadException` for a member referenced inside a prefix/postfix are raised when the CLR JITs that method, BEFORE its first instruction runs, so the method's own `try` block is never entered. The exception surfaces at the patched target instead, where `PatchShield.ShieldFinalizerWithResult` swallows it and the target returns `default(T)`. For a `bool`-returning gate that is `false`, which can invert the meaning of the whole feature. Worse, `PatchShieldPolicy.CompiledProtectedOwnerPrefixes` contains `"TAOM"`, so PatchShield refuses to unpatch the offender and the broken state persists for the entire session rather than self-healing.
- **Why missed:** the patch was written with a deliberate, documented try/catch and a comment reasoning correctly about fail-open behaviour for exceptions thrown *inside* the body. That reasoning is sound and complete for that class of exception, which is exactly why nobody asked about the other class. Uncapturable Heroes (2026-08-26) shipped one engine reference in a postfix body, `KillCharacterActionDetail.None`, that no binding test covered.
- **Prevent:** every engine member a patch body references needs a `BindingVerification` assertion, not just the patch TARGET. Enumerate the body's engine references (types, methods, properties, ENUM MEMBERS) and pin each. Treat "the try/catch makes this safe" as true only for runtime exceptions; ask separately what happens when the member cannot be resolved at all.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md finding 1.

### Harmony binds prefix/postfix parameters by NAME, so pin the names, not just the types
A binding test that resolves a target via `AccessTools.Method(type, name, new[]{ typeof(A), typeof(B) })` proves the method still exists with that shape. It does NOT prove the parameter NAMES are unchanged, and Harmony's injection binds by name. Parameter names are not part of a public API contract, so a rename is a legal, silent engine change; after one, the patch's parameters arrive as `null`/`default`, any null guard swallows every call, and the patch becomes a no-op that the type-resolving binding test and every behavioural test still pass.
- **Why missed:** TAOM's binding-test vocabulary was built to answer "does this member still exist", which is the right question for reflection sites and the wrong one for Harmony's name-injected parameters. Uncapturable Heroes (2026-08-26) had a passing type-based test over `TakePrisonerAction.Apply(PartyBase capturerParty, Hero prisonerCharacter)`.
- **Prevent:** for any patch whose prefix/postfix declares named engine parameters (not just `__instance`/`__result`), add a `CollectionAssert.AreEqual` over `method.GetParameters().Select(p => p.Name)`. Cheap, and it is the only assertion that can see this failure.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md finding 2.

### A patch whose whole purpose is to be unconditional must declare its Harmony priority
An unprioritised postfix runs at `Priority.Normal` with no ordering guarantee against other mods patching the same method. For a feature whose entire point is that a verdict cannot be overridden (here: these heroes are never capturable), another mod's postfix running afterwards can silently re-grant what TAOM denied, with no error and no log line. Declare `[HarmonyPriority(Priority.Last)]` so the denial is the final word (postfixes run highest-priority first), and keep the guard one-directional so a third-party mod can still make a hero LESS capturable but never more.
- **Why missed:** the design reasoned exhaustively about vanilla's call sites and not at all about co-resident mods. Cross-mod ordering is not in any standing review agent's rule set.
- **Prevent:** when a patch expresses a hard invariant rather than a tunable adjustment, state the priority and say in a comment which direction is deliberately left open.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md finding 3.

### Two seams that gate the same decision must carry the SAME guards, or they contradict each other
When a feature intercepts one decision at two points (here: a postfix on the capture gate and a prefix on the capture action), a guard added to one is a half-fix that reads as a whole one, because the reasoning written beside it is sound in isolation. TAOM's uncapturable-heroes postfix deliberately deferred to vanilla when `Hero.DeathMark != None`, to avoid stranding a hero the fugitive fall-through would not pick up. The prefix had no such guard, so vanilla answered `true`, `MapEvent.cs:1993` called `TakePrisonerAction.Apply`, and the second seam vetoed the capture the first seam had just decided not to veto.
- **Why missed:** the death-mark hazard WAS found by the first review round and the postfix guard was written for it, with a correct comment. Nobody asked the same question of the sibling seam. A review that confirms a guard exists is not the same as a review that enumerates every path reaching the guarded state.
- **Prevent:** when two patches gate one decision, list both guard sets side by side and diff them. Any guard present in one and absent in the other needs an explicit written reason. Add a test per guard on BOTH seams.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md finding 5 (Codex P1).

### A mutation performed before a notification must not be undone by the notification throwing
A prefix that returns `false` to skip vanilla, after already mutating campaign state, has a narrow but real failure window: if anything between the mutation and the return throws, the prefix's own catch returns `true` and vanilla runs anyway, on top of the state the mutation just wrote. TAOM's capture veto made a hero a fugitive, then read config outside the announcement's try/catch; a faulted `Lazy<T>` (which rethrows its cached exception forever) would have produced a hero the world was told escaped and who was then captured.
- **Why missed:** the announce call itself WAS guarded. The config read that decides whether to announce sat one line above the try, which looks like a gate rather than like work that can fail.
- **Prevent:** in any method that mutates and then reports, put the ENTIRE reporting section inside the guard, including the reads that decide whether to report. Test it with a throwing provider, not just a throwing presenter.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md finding 6 (Codex P2).

### A binding assertion on a NAME does not pin an enum comparison; pin the type and the numeric value
C# folds `x != SomeEnum.Member` to a constant at COMPILE time. A binding test asserting the enum type exists and contains a member of that name will still pass after the engine renumbers the member or changes the property's type, while the already-compiled guard silently compares against a stale literal and takes the wrong branch. Assert `property.PropertyType` identity and `Convert.ToInt32(Enum.Parse(t, "Member")) == expected`.
- **Why missed:** the assertion was added as the mitigation for a JIT-resolution hazard and was described as closing it. It closed the name half. Confidence attached to the artefact rather than to what the artefact actually checks.
- **Prevent:** for every engine enum a patch compares against, pin the declaring property's type AND the member's numeric value. More generally: when a test is offered as the mitigation for a named hazard, state the failure it can detect and check that it is the same failure.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md finding 7 (Codex P2).

### An IL call-presence test does not pin control flow, and must not be described as if it does
`IlCallScanner.ExtractCalledMethods` yields the calls a method makes, unordered by branch. Asserting that two calls both appear proves neither the branch relationship between them nor that the state they depend on survives to the second one. TAOM pinned "denying capture yields an escape" this way and described the premise as guarded; a refactor keeping both calls while removing the hero from the roster on the gate's false branch would have passed.
- **Why missed:** the test was genuinely useful (it catches deletion), and its usefulness was generalised into a stronger claim than it supports.
- **Prevent:** call-presence tests may claim "has not obviously been deleted", never "the premise holds". Add ordering where cheap; state the residual limitation in the test body and the feature doc so the next engine bump gets a manual read instead of a green light.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md finding 8 (Codex P2).

### When a patch body cannot be behaviourally tested, IL-assert that it still calls what it must
A Harmony patch taking sealed engine types cannot be unit-tested without a live campaign, which leaves whole mutation classes invisible: gutting a hook into a no-op, or deleting the engine call from an adapter that still returns success. `IlCallScanner` over the patch's own method body closes most of that gap cheaply, asserting the hook still calls its service and the adapter still calls the engine action. Also pin the patch-category LITERAL per hook, not merely that the hooks agree with each other.
- **Why missed:** ADR-008 exempts Harmony entry points from coverage, which is a reasonable rule that was read as "nothing here is testable" rather than "the usual technique does not apply here".
- **Prevent:** for every untestable entry point, ask what one-character change would break it silently, and pin that specific thing by whatever means is available. Inversion of a returned bool remains uncovered by this technique and belongs on the in-game smoke list.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md finding 9 (Codex P2).

### The decompile dump holds only core `TaleWorlds.*` assemblies, so "no call sites" there is not evidence of no callers

Grepping the whole of `E:\Decompiled_Bannerlord\_shipping_build_v1.4.8\` for `CharacterCreationManager.StartNarrativeStage` returned exactly one hit: its own definition. Read literally that says nothing in the game calls the method, which for a patch target is a reason to stop and rethink. The real caller, and the only one, is `SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView`'s constructor, in a MODULE assembly the dump does not contain at all. The dump's file list is `TaleWorlds.*.cs` plus a handful of third-party libraries; `SandBox.dll`, `SandBox.GauntletUI.dll`, `SandBox.ViewModelCollection.dll` and `StoryMode.dll` are absent. Every UI-adjacent engine method is therefore called from outside the dump.
- **Why missed:** the sibling lesson above ("grep every call site of the patched method") prescribes grepping the dump, and that technique is complete for engine-internal callers, so it looks sound right up until the target is reached from module UI. `taom-src` did not cover the gap either: it failed to resolve the view type by bare name, which reads as "this type does not exist" rather than "look somewhere else".
- **Prevent:** when a dump grep returns only the definition, treat the result as *unproven*, never as *no callers*, and decompile the module assemblies directly: `ilspycmd "<game>/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.GauntletUI.dll" > out.cs`. Where the call sits inside its caller can be load-bearing, not just trivia: here it established that the call happens before the stage ViewModel and `LoadMovie`, which is the entire reason a postfix that walks six menus forward produces no visible flicker.
- **Source:** Patch78 player-switcher career fast path, 2026-09-03.

### Substituting for a vanilla method inherits every one of its responsibilities; deferring to one inherits every one of its branches

Reading a vanilla method is directed by a question. Enumerating it is not. Patch80 made both mistakes in one changeset, in opposite directions. Seam B chose NOT to call `DecisionItemBaseVM.ExecuteDone` (it NREs on a cancelled election's null `_chosenOutcome`) and wrote a replacement close path from the two lines that mattered to that decision, silently dropping `ExecuteDone`'s third responsibility, `CampaignEvents.KingdomDecisionConcluded.ClearListeners(this)`. `OnFinalize` does not do it either and the listener list holds a STRONG owner reference, so every window the seam closed leaked the item view model, its option list and its election for the session. Seam A did the inverse: its catch deferred to vanilla `RefreshWith`, reasoned from the `else` branch where the bug lives, and never enumerated the `IsSingleClanDecision()` branch, which calls `GetChosenOutcomeText()` on a null `_chosenOutcome` and throws outright. The error path turned a hang into a crash.
- **Why missed:** the sibling entry "When a Prefix returns false, decompile the FULL call chain and replicate every safety gate" covers skip-original prefixes, so neither of these tripped it: one is a method we chose not to call at all, the other is a method we deliberately hand control back to. The existing "fall through to vanilla on error is only safe when vanilla is a safe default at THAT call site" rule WAS read and quoted in the offending comment, then applied to one of the two branches at that call site. Quoting a rule is not applying it.
- **Prevent:** before replacing, skipping, or deferring to a vanilla method, write down its branches and its side effects as a list, and mark each one replicated or consciously dropped with a reason. "I read it" does not discharge this; the list does. For a method you chose not to call, the list is its responsibilities. For a method you hand control back to, the list is its branches. Where the body cannot be behaviourally tested, pin each retained responsibility with an `IlCallScanner` call-presence test.
- **Source:** docs/reviews/rca-kingdom-vote-deadlock-2026-09-06.md findings 1 and 2, #547.

### A symptom is not a mechanism: ask what else produces the same player-visible failure

Patch80 root-caused an unclosable kingdom-vote popup completely and correctly, from a player report that named one trigger ("one vote follows another"). A second, entirely independent mechanism produces the identical symptom: TAOM's Player Switcher lets the player take over a non-leader member of a ruling clan, which leaves `Clan.PlayerClan.Leader` as the AI king, so vanilla's `Supporter.IsPlayer => Clan.Leader.IsHumanPlayerCharacter` is permanently false and every decision resolves through `ReadyToAiChoose()` inside the view-model constructor. All three of the shipped seams gate on `ShouldBeCancelled()`/`IsCancelled`, and that path never sets `IsCancelled`, so none of them fire.
- **Why missed:** the investigation was scoped to the changeset, which is correct for a review and wrong for a symptom. Five review agents all reasoned about whether the fix was correct; none could ask whether it was complete, because completeness here is a question about code that is not in the diff. It surfaced only because a user volunteered a second report mid-review.
- **Prevent:** when closing out a player-reported bug, state explicitly what else in the codebase could produce the same observable failure, and either rule each out with evidence or file it. For a UI state that has become unreachable/unclosable, the cheap sweep is: enumerate every write to the field that gates the exit (here `IsActive`, and the widget latch that starts the auto-close timer), and ask which code paths reach each write. Cross-feature interactions with identity-changing features (Player Switcher, enlistment, co-op) deserve the question by default, because they invalidate `IsHumanPlayerCharacter`-style assumptions engine-wide.
- **Source:** docs/reviews/rca-kingdom-vote-deadlock-2026-09-06.md finding 5, #550.

### An edge-triggered UI latch that is never reset is already set the second time you meet it

`KingdomDecisionPopupWidget.IsKingsDecisionDone` starts the popup's auto-close timer on its false-to-true edge and nothing ever writes it back to false; the widget outlives the view models it is bound to, one per decision. So "the window closes itself when the vote is over" is true exactly once per screen visit for any view model that is ALREADY over when it binds, and false for every later one: no edge, no timer, no close. Patch80's #550 route hit this on the second decision of a visit, which is why the player report reads "sometimes". The fix reads the VIEW MODEL's state at the moment the window is built (`IsKingsDecisionOver` right after `RefreshWith`) and closes through vanilla's own path, never relying on the widget edge.
- **Why missed:** the widget was read for what it does on the happy path (bind false, flip true, timer) and not for what it does on the second binding. A change-gated setter with no reset is a one-shot, and a one-shot bound to a reused widget is a latch.
- **Prevent:** when a close, dismiss or advance depends on a bool edge inside a Gauntlet widget, ask three questions before trusting it: is the widget reused across data sources; is the backing field ever reset; can the view model arrive already-true. If any answer is the wrong one, gate on the view model's own state at the seam that constructs it, and treat the widget edge as a bonus rather than the mechanism.
- **Source:** #550, Patch80 seam D, 2026-09-15.

### Bypassing a vanilla TRIGGER inherits what the trigger's own firing resets, not only what its callee does

Seam D applied the #547 lesson to the letter: it enumerated `ExecuteDone`'s responsibilities and inherited all of them by calling it. What it replaced was not `ExecuteDone`, though; it was the popup widget's five-second timer that normally calls it. That timer's firing does two things, and only one of them is `ExecuteDone`: the other is `_kingDecisionDoneTime = -1`. Closing early left the timer armed, and five seconds later the widget fired `ExecuteDone` again at whatever item was bound by then, a closed one (duplicate inquiry, asserted away) or a live one (NRE on a null `_chosenOutcome`). Codex review 113 found it; five Claude agents and the author, all reading the VM side, did not.
- **Why missed:** the lesson names "a method you chose not to call". A timer, an event edge or a queued callback is not a method, so the responsibility list was never written for it, and the widget was read only for how it starts the timer, not for how it ends it.
- **Prevent:** when a seam fires something EARLY (a close, an advance, a completion) that vanilla would have fired from a timer, an edge or a callback, open the thing that would have fired it and list what its own firing resets, clears or dequeues; then either reproduce that reset or make the eventual firing harmless. Seam E chose the latter, a single-shot guard on the callee, because the widget is out of reach from a view-model patch.
- **Source:** docs/reviews/rca-player-switcher-clan-leadership-2026-09-15.md finding 3, Codex review 113 F1, #550.

### A private field read as a success signal must be the LAST thing the success path assigns

Probe B of Patch79 postfixes `GauntletInformationView.OnShowTooltip` and read `_dataSource != null` as "the tooltip was built". The method's try block assigns `_dataSource` at line 113 and then calls `LoadMovie` at line 114; a throw from `LoadMovie` lands in the same catch as a constructor throw, leaves `_dataSource` set, and was therefore reported as success. `_movie`, assigned only after `LoadMovie` returns, is the field that actually means built, and reading both names the failing stage for free.
- **Why missed:** the try block was read as one exit ("the catch") instead of one exit per fallible statement, and "both silent exits leave it null" was written into two doc comments, the registry and the CHANGELOG before the code was reviewed. A count stated in prose is not evidence; the decompiled statement order is.
- **Prevent:** when a postfix infers an outcome from state, list every statement inside the try that can throw and write down which fields are already assigned at each one. Read the field assigned by the LAST fallible statement, or read more than one and classify. Then check the method's own reset (here `OnHideTooltip()` at entry) to confirm every field you read starts null.
- **Source:** deep-review of Patch79, 2026-09-04, data-flow agent trace 3; RCA `docs/reviews/rca-tooltip-diagnostics-2026-09-04.md` finding F2.

### Static cache state inside a Harmony patch is untestable by construction, so extract the decision

`Patch81_MarriageClanDraw` carried its candidate-pool dictionary, its invalidation stamp and the
build loop as private statics in the patch class. The stamp is the part with real logic and real
risk (the sentinel-collision shape that produced the shader-precompilation RCA), and inside a patch
class no test could reach it. Extracting `MarriageClanPoolStamp` as a pure class taking
`(campaignId, clanCount, day)` made it eight tests, one of which pins the exact `-1` sentinel meeting
a first real observation of `0`. The extraction also dropped the patch from 188 to 147 lines, but the
line count was the symptom, not the reason.
- **Why missed:** the cache was written where it was used, and ADR-002's line ceiling did not read as a signal because most TAOM `Hooks/` files are far larger (445, 358, 354). "Under 150" is a proxy; "no algorithms in an entry point" is the rule, and a cache with an invalidation policy is an algorithm.
- **Prevent:** when a patch needs state that outlives one call, name the invalidation policy and give it its own file before writing the dictionary. If you cannot write a test that makes the policy return true and then false, it is in the wrong class. Writing the extracted type's doc comment is itself worth the move: doing so here exposed a comment claiming the stamp caught `CultureConversion` re-cultures, when that feature converts SETTLEMENT cultures and never touches `Clan.Culture`.
- **Source:** deep-review of MarriageAlignment (#542), 2026-09-06; RCA `docs/reviews/rca-marriage-alignment-2026-09-06.md` findings 1 and 5.

### If a prefix writes shared state conditionally, the finalizer must clear it conditionally too

`KillCharacterAction_ApplyInternal_Patch` snapshots the victim and executor into a thread-local
`ExecutionContext` so the relation pass can read identity the engine is about to destroy. The prefix
was written re-entrancy-aware: it branches on `actionDetail` and only sets the snapshot for a real
execution. The finalizer was not, and cleared unconditionally. `ApplyInternal` re-enters itself,
because destroying the victim's clan calls `KillCharacterAction.ApplyByRemove` for every other living
hero in it (`DestroyClanAction.cs:43`), and clan destruction happens at `KillCharacterAction.cs:137`,
before `OnHeroKilled` fires at line 144. So a nested kill's finalizer wiped the outer execution's
snapshot mid-flight, for any executed lord with a surviving spouse, child or companion.
- **Why missed:** a prefix looks like a decision and gets asked "is this call one of mine?"; a
  finalizer looks like cleanup, answers no question, and never gets asked. The asymmetry is invisible
  because the two halves read as different kinds of code. The order-of-operations hazard inside
  `ApplyInternal` was studied closely (it was the bug being fixed) without ever asking whether
  `ApplyInternal` could appear on its own stack twice.
- **Prevent:** before writing shared state from a patch, establish whether the target can re-enter,
  and make the write and the clear agree on ownership. Harmony's `__state` is per-invocation and is
  the right tool; a depth counter is not needed. `KillCharacterAction`, `DestroyClanAction`,
  `ChangeKingdomAction` and `DestroyKingdomAction` are a mutually recursive cluster, so assume
  re-entrancy for any of them. Beware the near-miss that hides this: a second, independent guard in
  the same changeset (here a culture fallback that survives clan destruction) can make the wiped
  state still produce the right answer, so nothing fails until the two guards stop agreeing.
- **Source:** deep-review of the alignment-aware execution fall-through (#556), 2026-09-06; RCA
  `docs/reviews/rca-execution-alignment-fallthrough-2026-09-06.md` finding 1.

### A patch comment's RATIONALE is prose, and prose is where unverified claims hide
Four false statements shipped into a patch comment, a feature doc, a registry entry and a test
docstring in one session: "it also fires on a new game and on the initial data load" (the target has
exactly one call site, reached only on a saved-game load), "SubModule's guarded loop logs it and
carries on" (that call site had no try/catch at all), "by then vanilla has fixed what it can" (the
type overrides neither load hook), and "the reading rule is identical" for a reused token pair whose
meaning inverts between emit sites. All four were falsifiable by one grep. Three separate review
agents independently falsified the same one.
- **Why missed:** they were written as *design reasoning* rather than as factual description, which
  bypasses the reflex that fires on a bare claim. "This is why the seam is right" feels like
  knowledge; it contained an empirical assertion nobody had checked.
- **Prevent:** treat every "it also fires on X", "by then Y has happened", "the loop catches it" and
  "the rule is the same" in a patch comment as a claim requiring the same evidence as a signature.
  For call-site frequency specifically, enumerate callers across the installed assemblies (a
  `MemberReference` metadata scan, not a text grep) rather than reasoning from where you found the
  method. If a comment describes a guard, open the call site and confirm the guard exists.
- **Source:** docs/reviews/rca-stale-character-repair-2026-09-06.md findings 3-5.
- **Repeat (plan 012, 2026-09-24):** "the engine calls `DisableGlobalLoadingWindow` on every frame of
  the main menu, the party screen and character creation" shipped in a patch comment, a test
  docstring, the feature doc and the CHANGELOG. The list came from the log's top callers, not a
  caller census; the installed DLLs add the inventory, clan, kingdom, quests, character, crafting
  and banner-editor ticks. A plan's own prose is a claim too: when a plan quotes a call frequency
  for the builder to copy, the census belongs in the plan's evidence, or the prose says "several
  screens". Source: `docs/reviews/rca-loading-window-trace-per-frame-2026-09-24.md` finding 2.

### Campaign-event listener dispatch is LIFO: the LAST behaviour registered runs FIRST
`MbEvent<T>.AddNonSerializedListener` (installed v1.4.8, :24-30) HEAD-INSERTS each listener into a singly-linked list, and `Invoke` :32-35 walks from the head. TAOM adds its campaign behaviours in `SubModule.OnGameStart`, deliberately after SandBox has added its own, so **every TAOM `CampaignEvents` handler runs BEFORE the vanilla handler for the same event.** Any TAOM handler that mutates shared engine state on a campaign event is therefore mutating it out from under vanilla's handler on that same dispatch. The intuition that "we load after them, so we run after them" is exactly backwards.
- **Why missed:** the Patch84 investigation (#557) needed to know whether TAOM's enlistment detach ran before or after vanilla's siege-aftermath handler. It inferred the order from module load order, never opened `MbEvent`, concluded vanilla ran first, and used that to RULE OUT TAOM's own detach as the cause. The conclusion was written into a code comment, two docs, the CHANGELOG and a GitHub issue before `/deep-review`'s data-flow agent decompiled `MbEvent` and inverted it. Four other agents could not have caught it: the changeset never calls `MbEvent`, so it was outside every "verify the APIs this code uses" scope.
- **Prevent:** never infer campaign-event dispatch order from module or registration order; it is inverted. When a TAOM handler and a vanilla handler share an event and one mutates state the other reads, assume TAOM's runs first and design for it. Concretely: `MainParty.AttachedTo = null` inside a `MapEventEnded` handler removes the party from `MapEventSide._battleParties` (via `SetAttachedToInternal` :1780-1783 -> `HandleMapEventEndForPartyInternal` -> `RemovePartyInternal`) before any vanilla `MapEventEnded` handler evaluates `IsMainPartyAmongParties()`. That is the #551 seam and it produced #557 too.
- **Source:** docs/reviews/rca-siege-aftermath-menu-guard-2026-09-07.md, #557.

### Draw a prefix's point of no return at the first statement that breaks a vanilla precondition, per statement, never at the top of the block
The two error-path lessons above say WHETHER a catch may defer to vanilla. They do not say WHERE the line goes when the replacement is several statements and only one of them breaks something vanilla reads. Patch87 (#566) replicates vanilla's five-statement Leave; only `PlayerEncounter.LeaveSettlement` breaks a precondition (it nulls `CurrentSettlement`, `LeaveSettlementAction.cs:27`, which vanilla's own body dereferences at :371). The first draft put the "no return" boundary at the top of the block, so a throw on the gate-position write, which changes nothing vanilla reads, would have skipped vanilla and logged "its body would now dereference a null", which is false for that statement.
- **Why missed:** the sequence was copied from vanilla as one unit and guarded as one unit. Skipping vanilla more often is the safe direction for crashes, so the over-wide boundary looked conservative. It is the wrong direction for the log: the next reader would hunt for a null that was never there.
- **Prevent:** before wrapping a replicated sequence, list each statement and write beside it which vanilla precondition, if any, it breaks. Put the deferrable statements (none broken yet) in the first block with a `return true` catch, and start the skip-vanilla block at the first statement that breaks one. Say in the comment WHICH statement that is and WHAT it breaks. A data-flow review question that catches the miss: "if statement N throws, is vanilla still safe?", asked once per statement.
- **Source:** `docs/reviews/rca-return-to-army-2026-09-12.md` finding 1 (deep-review data-flow agent, trace 9), #566.

### A postfix on an order setter sees the engine's housekeeping too; enumerate the callers before treating an order as intent
`Formation.SetMovementOrder` has 63 call sites in the installed v1.4.8 engine. Besides `OrderController` (the player) and the team-AI behaviours (gated on `IsAIControlled` in `FormationAI.TickOccasionally`, `FormationAI.cs:284-290`), the engine calls it to re-issue a formation's CURRENT order when a banner bearer dies (`BannerBearerLogic.FormationBannerController.RepositionFormation`, `BannerBearerLogic.cs:157`), to substitute a plain Charge when a `ChargeToTarget` target empties (`Formation.Tick`, `Formation.cs:2291-2295`), to convert an `Invalid` input into Stop (`Formation.cs:688-691`), and to route a whole side (`Team.Tick`). SmartCavalryAI v2's first cut read every non-charge order on a mid-cycle formation as the player displacing the machine and cancelled; a bearer death mid-line-up would have ended the cycle with no player intent behind it.
- **Why missed:** the design enumerated the player's order kinds and the AI's, which are the two callers a reviewer thinks of, and the sibling lesson "grep every call site" was applied to the player path only. The five deep-review agents that read the changeset could not see it; the compatibility agent found it because its brief asked "who ELSE calls the target" and it decompiled the whole assembly.
- **Prevent:** for any prefix/postfix on a setter the engine also drives, list every caller from a full-assembly decompile (`ilspycmd -p`), classify each as player, AI, or housekeeping, and write down what the patch does for each class. To tell a re-issue from a new order, capture the previous value in a prefix (`out T __state`) and compare with the engine's own equality (`MovementOrder.AreOrdersPracticallySame`) rather than by enum alone. Pin the captured member and the comparison method in the binding test.
- **Source:** docs/reviews/rca-smart-cavalry-2026-09-13.md, #586 (deep-review compatibility agent, caller enumeration (i)/(j)).

### A seam that lacks the actor has more than one caller: list them, with the actor each supplies, before writing "always"
`ExecutionCampaignBehavior.GetBloodFeudStartRelationPenaltyToOtherClan(Hero dyingHero, Clan otherClan)` (v1.5.2) carries no executor. The v1.5.x re-seam arrived at it from `OnPlayerExecutedHero`, which is player-only, and wrote "only the player executes through this path" into the patch, the registry and the feature doc. The method's other caller is the same loop in `OnBloodFeudStateChanged` on the `StartedByAIExecutePlayerRelative` detail, reached from `OnHeroKilled` -> `OnPlayerClanMemberExecuted` when an AI clan executes a member of the player's clan. There the hardcoded executor put the player's own side on both ends and the kinslaying multiplier fired against the bereaved.
- **Why missed:** the trap the 2026-09-06 entry above names ("reasoning from where you found the method"), one level up. The port enumerated the callers of the seam it was leaving (`OnLordExecuted`, one caller) and not of the seam it was moving to. The v1.4.8 design had recognised that `OnLordExecuted` lacked the actors and bridged them with a snapshot; the re-seam kept the conclusion and dropped the question. The claim was written as design reasoning, which does not trigger the reflex a bare fact does.
- **Prevent:** when a patch supplies an actor the target's signature does not carry, list every caller of the target from the installed assembly beside the claim, naming the actor each supplies, and make the path decision a hook method with tests (`IOnExecutionAction.IsPlayerTheBereaved`). `.claude/rules/harmony-patches.md` "Research First" now asks for the list. Two review agents with the decompile open found this in one pass; the doc had made the claim without it.
- **Source:** `docs/reviews/rca-v1.5.2-compile-2026-09-14.md` finding 1 (deep-review data-flow agent trace A and the Execution audit, independently).

### Cost a guard's failure path as the loop it is, not as the once it feels like

A guard is written for the case where it does nothing and is therefore costed for that case. Patch90's drain polled a set of body names with a 1 ms sleep for a 5 s budget and evaluated `pending.RemoveAll(name => Resolves(isResolved, name))` inside the loop; the lambda captures a parameter, so Roslyn allocates a delegate per evaluation, about five thousand across the budget on exactly the path the guard exists for. On the healthy path it is one allocation and invisible.
- **Why missed:** the failure path was reasoned about as "five seconds once" rather than as five thousand iterations of a loop body, and the closure rule in the efficiency checklist names per-frame loops, which a `Thread.Sleep(1)` polling loop is in everything but name.
- **Prevent:** when a patch has a bounded wait, write down the iteration count the budget implies at the sleep granularity, and read the loop body at that multiplier: delegates and closures hoisted before the loop, no allocation per pass, a try/catch per item only where a throw is expected and swallowing it is the design. The efficiency agent's per-frame closure check applies to any loop whose bound is time rather than count.
- **Source:** `docs/reviews/rca-preload-body-guard-2026-09-15.md` finding 1, #601.

### A caller-driven guard's coverage is the caller's coverage, so read each caller's gate before saying when the guard runs

Patch90 prefixes `PreloadHelper.WaitForMeshesToBeLoaded`, which has six callers. Five call it once per mission from `OnSceneRenderingStarted`; `GauntletEducationScreen` calls it from `OnFrameTick`, and the feature doc described that as a per-frame caller. The screen gates the call behind its own `_startedRendering` latch, so the wait runs once per screen instance, and the character sets that later option picks preload into the same helper are never waited on by vanilla and so never guarded. The doc implied a coverage the guard cannot have.
- **Why missed:** the caller list came from a grep of the decompile for the method name, answering "does the patch attach" and not "what does each caller do around the call". The lesson "grep every call site before documenting when a patch runs" was applied to the grep and not to the read.
- **Prevent:** for every caller of a patched method, read the statement that makes the call and the condition that gates it, and describe the guard's coverage in those terms (once per mission, once per screen, every frame). A prefix guards exactly the calls that would have reached vanilla; anything vanilla does not call is outside the guard by construction and the doc should say so.
- **Source:** `docs/reviews/rca-preload-body-guard-2026-09-15.md` finding 3, #601.

### A callback's thread is native's choice: a table row saying "main thread" is a claim until a log line backs it

The #595 audit classified `Mission.OnAgentRemoved` and `OnAgentDeleted` as main-thread callbacks because the thread-map row said so, and reviewed every TAOM `OnAgentRemoved` handler under that assumption. Both are `[MBCallback]`s with no managed caller, so no decompile can show their thread. A v1.4.8 player log from the guard that audit added caught `OnAgentRemoved`, `OnObjectUsed`, `OnObjectStoppedBeingUsed`, `OnAgentShootMissile`, `OnAgentDismount` and `OnAgentAlarmedStateChanged` on worker threads, leaving `BehaviorTreeAgentComponent.OnAgentRemoved` and `MountDespawnMissionBehavior.OnAgentRemoved` writing main-thread collections unsynchronised.
- **Why missed:** the row was written from reasoning ("native combat processing, on the main thread"), then treated as verified because it sat in a table titled "verified on v1.4.8". The one callback proven off-thread (`OnAgentPanicked`) was proven through a managed route; the absence of a managed route for the others was read as evidence of main-thread delivery instead of as "unknown".
- **Prevent:** for a native-raised callback, the thread column says "native decides" unless a runtime observation (a `MissionThreadGuard` report, a hang-dump stack) pins it. Any mission-time state an engine callback writes goes through `DeferredCallbackQueue.RunOrDefer` or a lock by default, not only when a route to a worker has been found. And when a tripwire is added, the first player log it produces gets read against the table it was meant to confirm. The correction states only what the evidence shows: the #634 fix first wrote "on a worker" (the thread ids cannot say which off-main thread) and "native serialises these with the agent tick" (an inference from unlocked vanilla handlers) as facts, in the same table it was correcting, and the deep review had to take both back out.
- **Source:** `docs/reviews/rca-offthread-agent-removed-2026-09-22.md`, #634.

### A value-returning finalizer that hands back its exception erases the throw site

When any finalizer on a method returns a value, Harmony 2.4.2's wrapper stores it in the one shared exception slot and ends with the `throw` opcode; only a method whose finalizers are ALL `void` gets `rethrow`, which keeps the trace (`MethodCreator.CreateReplacement` / `AddFinalizers`). Throwing an existing exception object replaces its stack trace with the frames from that point outward, so every frame between the real throw site and the patched method is gone by the time anything upstream reads it. PatchShield hands every non-trinity exception back this way on every patched method in the process (1,476 in player bundle `2d446100`), so a childbirth failure several calls inside the daily pregnancy tick reached the crash report as five frames ending at `MapState.OnTick_Patch2`, and the throw site could not be named. The same collapse merged distinct crashes: the signature hashes `new StackTrace(ex)`, which sees only the segment after the last throw, so three NullReferenceExceptions through that frame shared bundle `40de8e64` and two were suppressed unread.
- **Why missed:** the reset was known and written down twice (`PatchShield.cs` for #354, `SaveShield.cs` for its attribute-once rule), and both times it was fixed only for the case in hand: #354 stopped unwrapping `TargetInvocationException`, which rescues the INNER exception's stack and leaves a plain exception's own frames exactly as exposed as before. A limitation recorded as a comment reads as handled.
- **Prevent:** a finalizer that only observes should be `void`, which leaves Harmony on `rethrow`. One that returns an exception writes `return RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod);` (or `null` for the method on a hot target, rather than binding `__originalMethod` per call). The helper moves the trace into `_remoteStackTraceString` (what .NET's `InternalPreserveStackTrace` does), prints a marker line naming the rethrowing method, records the original frames under `Exception.Data["TAOM.ThrowSite"]` for frame-based consumers, returns the same instance, and is idempotent per rethrow. PatchShield and SaveShield do this. Ten TAOM feature finalizers do not yet (plus the crash reporter's ten on their fallback paths), and PatchShield does not cover the late-batch ones on a process's first game (see the next entry). `RethrowStackPreserverTests` pins the premise with a real Harmony patch, so a Harmony upgrade that starts preserving turns it red instead of double-printing.
- **Source:** `docs/reviews/rca-shield-rethrow-stack-2026-09-22.md`, player bundle `2d446100`.

### A pass that enumerates "everything patched" covers only what was patched before it ran

PatchShield shields `Harmony.GetAllPatchedMethods()` at two moments: pass 1 in TAOM.Dependencies' `OnSubModuleLoad`, pass 2 in its `OnGameInitializationFinished`. The engine calls `OnGameInitializationFinished` per module in load order, and TAOM depends on TAOM.Dependencies, so TAOM's own late-batch categories (applied once per process in TAOM's `OnGameInitializationFinished`: `Patch65`, `Patch88`, `Patch69`, `Patch77`, `Patch56`, `Patch20`, `Patch50`, `Patch43`, `Patch13` and more) are patched AFTER pass 2 on the first game and stay unshielded for that whole game. Pass 2 has no once-guard, so the second game init of the process shields them. A statement like "PatchShield covers every patched method" is true only from the second game on.
- **Why missed:** the shield's coverage was reasoned from what it enumerates, not from when it enumerates relative to the patching it is meant to cover. The sibling entry "Apply a patch's category at a lifecycle point that PRECEDES the earliest render" is the same shape from the patch's side; this is it from the enumerator's side. It surfaced only because two deep-review lenses read `Dependencies/SubModule.cs` and the engine's module dispatch in one pass, and the player bundle then showed the shield on `MapState.OnTick_Patch2` in a session that had reloaded a save.
- **Prevent:** for any mechanism that acts on "all X that exist" (all patched methods, all loaded types, all registered behaviours), write down when it runs and list what is created after that moment on the first run, in the same sentence as the coverage claim. For PatchShield specifically: a crash in the first game of a process may still carry a truncated trace from a TAOM late-batch finalizer, and that is expected until those finalizers preserve their own rethrows.
- **Source:** `docs/reviews/rca-shield-rethrow-stack-2026-09-22.md` (deep review findings 2 and 9).

### Hardening a defect class covers every member of the class, not only the members that could produce the symptom

The #634 audit listed `CareerPerkMissionBehavior.OnAgentRemoved` (clears a `List` the mission tick walks, recomputes other agents' stats) and set it aside because "a List cannot spin", the freeze's suspected mechanism. The deep review then found four more writers of the same class the change had not hardened: `SpatialGrid.Remove` from `OnAgentDeleted`, the SignatureStrikes roster, the Enlistment kill count, and the Warg dismount reset, the last on a callback the player's log had already proven off-thread. The RCA and CHANGELOG had shipped "three writers".
- **Why missed:** the census was filtered by the incident's mechanism (what could corrupt a `Dictionary` into a spin) instead of by the defect class the fix was defining (engine-callback state written off the main thread). And the census predated the thread-map change: once `OnAgentDeleted`, `OnAgentDismount` and `OnAgentHit` moved to "native decides", their handlers were never re-enumerated.
- **Prevent:** when a change defines a rule ("route engine-callback writes through `RunOrDefer`, a lock or a concurrent collection"; and before swapping a `Dictionary` for a `ConcurrentDictionary`, check whether any reader depends on insertion order, a stable sort's tie-break included), apply it to every handler the rule covers in the same change, or list the ones deferred with a reason. After moving any row of the thread map, re-run the handler census for every callback whose row moved: `override void On(Agent|Object|Score|Melee|Missile)` across `Main/`.
- **Source:** `docs/reviews/rca-offthread-agent-removed-2026-09-22.md` findings 1-2, #634.

### A stall probe brackets the caller that owns the whole frame, and keys its quiet period on engine state, not on a TAOM latch

Patch91 first bracketed `Mission.OnTick`, the managed tick TAOM knows. Its caller, `MissionState.TickMissionAux`, runs the native `Mission.Tick` first, and that is where `OnPreTick`'s wait and every native-raised agent callback execute on the main thread, so a handler spinning there would have produced no `[MissionStall]` line. And a mission's teardown runs inside `Mission.OnTick`, so the probe stood in flight through every exit; the stand-down was keyed on the exit-window latch, whose opener fires only for campaign missions with the master toggle on. Custom Battles and master-off campaigns would have logged a long exit as a frozen battle.
- **Why missed:** the probe site was chosen from the method name, without reading the caller that makes it one step of a frame; the quiet period reused the nearest existing latch without checking its opener coverage, the exact failure mode of the "Latches & Toggle Gates" rule's first point.
- **Prevent:** before bracketing a tick, open its caller and bracket the call that owns the whole unit of work; list what runs inside it that is not the tick itself (teardown, loading) and gate those out on the engine's own state (`CurrentState == Continuing`), which every mission type sets, rather than on a TAOM latch with a conditional opener.
- **Source:** `docs/reviews/rca-offthread-agent-removed-2026-09-22.md` findings 3-4, #634.

### A report delegate runs on the thread that raised the event: send it to the locked file log, never to an on-screen logger

`MissionThreadGuard.NoteCall` invokes its report synchronously, on the off-main thread it is reporting. `BehaviorTreeMissionLogic` passed `BTRegister.Logger.LogMessage`, and `BTRegister.Logger` is whichever logger won an initialisation race: `BehaviorTreeBannerlordWrapper.Instance` installs `BannerlordLogger` (`InformationManager.DisplayMessage`, a UI call) on first access, and only `WargMissionBehavior` and the creature behaviors install the file-backed `TaomBTLogger`. A battle without them would have posted a UI message from a worker thread for every parked callback site.
- **Why missed:** the reporter was chosen for where its text lands (the player's log in the sessions that were read), not for which thread calls it; the logger slot's value depends on behavior order, which no test pins.
- **Prevent:** any callback that can run off the main thread reports through `IModLogger` (`FileLogger` takes a lock) at WARNING, wrapped so a reporting failure can never throw into an engine callback. Never pass an on-screen or UI-backed logger as a report delegate.
- **Source:** `docs/reviews/rca-offthread-agent-removed-2026-09-22.md` finding 7, #634.

### When a postfix sees the same state after a real call and a no-op, capture the pre-call value in a Prefix through `__state`
`LoadingWindow.DisableGlobalLoadingWindow()` sets `IsLoadingWindowActive = false` whenever a
manager exists, whether or not the window was up, and the engine calls it every frame from most
full-screen menus. A postfix alone therefore sees `false` after both a real lower and a no-op, and
the v2.0.29 trace wrote one stack walk and one flushed line per rendered frame (84 MB in 35
minutes, 1.16 GB in three hours on the main menu).
- **Why missed:** the patch was written for "a handful" of transitions and assumed the target was
  called only on transitions; nobody enumerated its callers or read that the clear sits outside
  the `IsLoadingWindowActive` branch.
- **Prevent:** before logging from a postfix on an engine setter-like method, read whether the
  method writes its state unconditionally and list its per-frame callers. If it does, capture the
  pre-call value with `Prefix(out T __state)` and decide in the Postfix from (before, after). Keep
  the decision in a pure gate so the unreachable cells (a null manager, a skipped original) are
  unit-testable, and keep the trace call directly in the Postfix when a tracer skips frames.
- **Source:** plan 012, `docs/reviews/rca-loading-window-trace-per-frame-2026-09-24.md`, `LoadingWindowDisablePatchTests`, `LoadingWindowTraceGateTests`.
