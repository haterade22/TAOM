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

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
