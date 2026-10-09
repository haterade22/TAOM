# RCA: yotthani adoption, items 1 to 4 (deep review, 2026-10-08)

## Top-line

The `/deep-review` of the four ported VanillaTuning features (mission-start guard, skeleton-buffer guard and watch,
nameplate cull, map-view release) ran seven lenses on the uncommitted worktree `E:\repos\taom-yotthani`. It found
no HIGH or CRITICAL defect. It found one real engine gap (a second unbounded skeleton pool), one silent-loss path
already recorded as a lesson (PatchShield strips Patch103), a wrong coexistence rule with VanillaTuning, a set of
repo-wide test gates the new code had not joined, and doc claims that were not measured or were wrong, two of them
the orchestrator's own. Every finding below was checked against the code, the v1.5.4 decompile or the installed
binary before it was fixed. The two decisions it raised for the maintainer were taken on 2026-10-08 (end of this
file).

Review record: [adopt-yotthani-2026-10-08.md](adopt-yotthani-2026-10-08.md). Lenses: 1 Standards, 2 Engine
compatibility, 3 Efficiency, 4 Completeness, 5 Data flow, 6 Design, 7 XML.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The guarded function first reserves from a second per-frame pool (`[0xD9D160]+0xC28`, `FUN_18006af30`, `lock xadd` at `0x6AF58`, 262,144 entries) with the same missing bound; nothing guarded or watched it (Lens 2 F1, verified by disassembly) | Native research stopped at the site upstream named | The re-derivation decompiled the reservation yotthani pointed at and confirmed his claim; nobody surveyed the rest of the function for the same pattern | Watch both pools now; lesson "A native guard covers every reservation of the same shape in the function, not only the one upstream named" (`lessons/native-cpp-port.md`); a second guard is the maintainer's decision |
| 2 | MED | From the second game start PatchShield can strip Patch103's prefix and transpiler (owner `com.taom.mod` is not protected); nothing logged it (Lens 5 #1) | Silent loss of a patch: a REPEAT of the 2026-10-03 lesson | The builder brief did not carry the lesson; the module logs once at install | A canary: the prefix marks the mission, the finalizer (never stripped) warns once per process when the prefix did not run or the live swap count is not 6 |
| 3 | LOW | The start guard hid its own notice when VanillaTuning was loaded, but TAOM's call-site wrap only ever catches what VanillaTuning's per-method finalizers missed, so the player saw nothing (Lens 5 #3) | Coexistence reasoned from a module's presence | The brief copied the shader-notice precedent (two windows) without tracing which guard sees the exception first | Delete the branch; one rule recorded in the review: detect the other mod's effect, not its presence |
| 4 | LOW | The skeleton guard stood aside on VanillaTuning's module id before the byte check that exists for this case, so a VanillaTuning with its guard off left nothing guarding and TAOM's watch off (Lens 5 #10) | Same as 3 | Same as 3 | Delete the module-id check; the foreign-jump check decides |
| 5 | MED | The per-frame cull settings provider was not in the campaign hot-path settings gate (Lens 1 #1) | A repo-wide gate not joined | The feature's own tests pass; the gate lives in another folder and the brief did not name it | Lesson "A new per-frame or per-hit settings provider joins the hot-path provider gate in the same commit" (`lessons/testing-qa.md`); the gate rows added |
| 6 | MED | A binding test that runs engine code lacked `RequiresGameIL` (Lens 1 #2) | A repo-wide gate not joined | Same as 5 | Tag added; same lesson |
| 7 | MED | Four name-based private field accesses and the six swap targets had no rows in the reflection-site catalogue (Lens 2 F2) | A repo-wide gate not joined | Same as 5 | Rows added; same lesson |
| 8 | MED | Patch103's prefix and finalizer and the watch behaviour's start and tick had no tests (Lens 4 F4) | Entry points untested | The builders tested the services and pure parts, and pinned the hooks by shape only | Hook tests added; covered by the existing lesson "Tested indirectly is not coverage" |
| 9 | LOW | The skeleton-buffer tests were never run RED (Lens 1 TDD) | A REPEAT of "If you didn't watch the test fail, mutate the code until it does" | The builder wrote tests first but ran the suite only once the code existed | Four mutations run and reverted, each caught (two in the skeleton buffer, two in the nameplate cull); the lesson's recurrence appended |
| 10 | LOW | `[NameplateCull]` window line: counted wall time with the toggle off, battles and menus, and the documented one-minute A/B could never see it (300 s window) (Lens 5 #5, Lens 4 L1) | A log line's meaning not traced to its consumer | The line was written for the code, the A/B procedure for the doc, and nobody read one against the other | Window counts culled frames only; the A/B reads `[MapProfile]` |
| 11 | LOW | Docs: the guard-off in-game step expected an endless loop the QA trigger cannot produce (it resets before it throws); the load-game check ran before the patch existed; "complete in the same frame as vanilla" was one frame off (Lens 5 #4, #6, #7; Lens 2 F5, F7) | In-game steps written without tracing the trigger | Each step was written from the feature's intent | Lesson "An in-game check names the log line it expects and the code path that writes it, traced through the trigger" (`lessons/testing-qa.md`) |
| 12 | LOW | Section 10 said 630 references to the global; there are 210 (the orchestrator's scan counted each match at three overlapping offsets) (Lens 2 F3) | A count from one ad hoc method | Written straight from the script's output without a second method | Count re-taken two ways; lesson "A count in a reference doc names its method and is taken two ways" (`lessons/native-cpp-port.md`) |
| 13 | LOW | "a 10 MB native function" (the `.text` size), "28 entries" stated as fact, "one frame of bad animation" (Lens 2 F4, F8) | Unverified claims written as fact | The builder summarised the engine doc from memory | Fixed with their basis; same lesson as 12 |
| 14 | LOW | `tools/native_sig_author.py xref` sweeps 2.8 % of `.text` and was still recommended without a caveat (Lens 4 F2; found by the orchestrator) | A pre-existing tool defect | Its output looked plausible (16 hits) | Caveats in three docs; issue draft 6 |
| 15 | LOW | `/engine-bump` did not list the new per-build pin or ask for the guard's line (Lens 4 F3) | Harness checklist not updated | New native pins are rare | Both added to the skill |
| 16 | LOW | INDEX and the adoption precedent said formation presets were "finished" (item 5 is not built), and the precedent still described the plan's `AddMissionBehavior` design (Lens 4 L3) | The orchestrator wrote summaries ahead of the work | Written before the builders reported | Corrected; covered by `evidence-over-claims.md` C.1 |
| 17 | LOW | Smaller code items: a closure allocated on every scene-layer switch; an unused `ForeignSiteRva`; a private copy of `FiniteFloatValidator.IsFinite`; transpiler pieces outside `Hooks/`; a list-adjacency pin with no reason; a source scan with no floor; a stale count in a comment; a generic enum name in a shared namespace (Lens 3, Lens 1, Lens 6) | Polish | Within normal review scope | Fixed |
| 18 | LOW | Doc and register hygiene: the four feature lines filed under Sieges; settings catalogues not updated (`crash-report.md`, `mcm.md`); the lifecycle trap doc silent on Patch103; provenance and the notice missed the adapter and test files; two harvest blocks and a stray blank line in `taom_module_strings.xml` (Lens 4, Lens 1 #8, Lens 7) | Completion duties | The builders updated the feature's own docs, not the catalogues that list it | Fixed; the harvest tool's own bug is issue draft 7 |

## Root-cause patterns

**A port inherits upstream's scope.** Findings 1, 3 and 4 share it. yotthani's guard covers the site he found, and
TAOM confirmed that site instead of asking what else in the function has the same shape. His coexistence choices
(a module-id check, a suppressed second notice) were copied as precedent without tracing which guard actually sees
the event. The rule: confirm the upstream claim, then survey around it (the whole function, the whole call path),
and decide coexistence by the observable effect at the shared point.

**Feature-local green is not repo green.** Findings 5 to 8 passed every test in their own folders. The gates they
missed (hot-path providers, reflection-site rows, `RequiresGameIL`, entry-point tests) live elsewhere and fail only
when someone reads them. The briefs named the feature's tests and the settings counts, not these gates.

**Claims ahead of measurement.** Findings 11 to 13 and 16 are statements written before, or without, the evidence
that would make them true. Two are the orchestrator's own (the count, the "finished" summary).

## Why each lens caught what it caught

- **Standards** found the gate gaps (5, 6) and the polish, and judged the skipped RED run (9).
- **Engine compatibility** found the second pool (1) by disassembling the whole guarded function, and corrected the
  count (12) with two independent scans.
- **Data flow** traced PatchShield into Patch103 (2), modelled the two guards' order (3, 4) and the A/B line (10).
- **Completeness** found the untested entry points (8), the tool caveat and the harness gaps (14, 15) and the
  orchestrator's wrong summaries (16).
- **Efficiency** proved the hot paths allocation-free from the built IL and found the one closure (17).
- **Design** replaced Patch105 with an engine event, made the strip check a canary, and showed `__runOriginal`
  would be dead code on the shipped Harmony 2.4.2.
- **XML** found the harvest block duplication (18) and its cause in the tool.

## Lessons appended

- `lessons/native-cpp-port.md`: survey the whole function for the defect's shape; a count names its method and is
  taken two ways.
- `lessons/testing-qa.md`: a new per-frame settings provider, name-based private access or engine-executing
  binding test joins its repo-wide gate in the same commit; an in-game check names its log line and traces the
  trigger; the recurrence of the mutation lesson.
- `lessons/harmony-il.md`: the recurrence of the PatchShield strip lesson, with the canary as the cheaper check
  for a patch whose finalizer survives the strip.

## Second round: the fixes re-reviewed, then the final state (2026-10-08)

After the first round's fixes and the second-pool guard, a re-review (lenses 1, 2, 4 and 5 on the pool-2 increment)
and a final-state review (lenses 1, 2 and 5 per feature, 3 and 6 over all four, a completeness critic) ran. Every
finding below was checked against the code, the v1.5.4 decompile or the installed binary before it was fixed; the
HIGH one was confirmed at the bytes (`0x34B769` stores 0 to the view's ready word, `0x50AB20` returns it). The
workflow's skeptic stage was skipped after the lenses: the orchestrator verified each finding instead.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 19 | HIGH | The map-view release ran on the cover, inside `ScreenBase.HandlePause` before `MapScreen.OnPause`; native `ClearAll` zeroes the view's ready word, so `OnPause` raised the global loading window over the covering screen, and Options, Save/Load and TAOM's own screens never lower it (final lens 2) | An engine call's side effect not traced to the code that runs next | The port kept upstream's timing; the feature doc's own in-game step had flagged the "not ready" effect as unverified, and nothing checked it before the code was called done | Release on the map layer's return instead (pending mark at the cover); lesson "A native call's side effects decide when it may run" (`lessons/native-cpp-port.md`) |
| 20 | MED | Patch103 wraps TAOM's own `SubModule.OnMissionBehaviorInitialize`: a survived throw there skipped the BattleLoad loading window's only closer, so the window stayed open for the battle and the stall watchdog would bundle a running battle (final lens 5) | A guard that changes a failure mode, not traced through the cut-short code | The design took "TAOM's behaviours guard themselves" from #699 and never asked what the method still had to do after a throw | The closer is registered in a `finally` around all of TAOM's mission wiring (after findings 34 and 35), pinned by `BattleLoadCloserWiringTests`; lesson "A guard that turns a throw into survival keeps the code after the throw that cannot be skipped" (`lessons/harmony-il.md`) |
| 21 | LOW | The start guard's canary kept "this call's prefix ran" in a process-wide flag; a finalizer rerun after a later finalizer throws would fake a lost guard and spend the once-per-process latch (final lens 6) | A REPEAT: the 2026-10-03 lesson "pair a prefix with its finalizer through `__state`" | The builder brief did not carry the lesson, and the canary was tested with hand-called begins and ends, not through real Harmony | Moved to `__state`, with a real-Harmony rerun test; one rule line in `.claude/rules/harmony-patches.md` (loads with every patch file), the recurrence appended to the lesson |
| 22 | LOW | Install lines claimed ON without checking the patch applied: Patch103's swap count survives a failed apply (the transpiler runs before Harmony can still fail), and Patch104's line never asked Harmony (final lens 5, both features) | A log line's claim not derived from the checked state | `ModuleRunner.RunPhase` calls `OnPhase` whatever the category's result, and both lines were written there | A `[HarmonyCleanup]` resets the count on failure; the cull asks `Harmony.GetPatchInfo`; lesson "An install line claims only what was checked" (`lessons/harmony-il.md`) |
| 23 | LOW | The engine's per-frame allocator at `[0xD9D160]` holds at least eleven pools with the same missing bound, two more on the skeleton draw path (orchestrator's survey after re-review lens 2) | Survey scope | The first round's lesson surveyed "the function"; the allocator is wider | Documented with addresses and sizes (engine doc section 10), not guarded; issue draft 9; the recurrence appended to the native lesson |
| 24 | LOW | Smaller code items: an int32 wrap in the pool-2 call-link check; a throwing site write logged as an unpooled line; the WARNING level still keyed to the old unguarded pool 2; a duplicate constant and misleading names; a null owner never reported by the start guard; a test that never called the method it named; the park point and `RefreshBindValues` not pinned; a culled-time window that a frame count does more simply (re-review lenses 1, 2, 5; final lenses 1, 5, 6) | Polish and pins | Within normal review scope | Fixed, each with a test |
| 25 | LOW | Tests of two settings providers sat in `RequiresGame` binding classes, so no no-game run executed them (final lens 1) | A REPEAT of finding 5's category | The tests were placed with the binding tests of the same feature | Moved to the untagged provider classes; the recurrence appended to the testing lesson |
| 26 | LOW | Player-facing text stated upstream figures as TAOM facts: the cull hint's 1,002 settlements (the live map has 1,040) and the map-view hints' measured targets and timings (final lenses 1, 2, 5) | A REPEAT of finding 13's category | Hints were written from the source mod's notes | Hints rewritten and attributed; the recurrence appended to the count lesson |
| 27 | LOW | Docs and comments: the guard-off in-game step left out TAOM's crash capture; the coverage claim missed `OnMissionScreenPreLoad` and the mod-replaceable deployment plan and weather model; the census and registry wording; the lifecycle chart (pre-existing, wrong for behaviours added in `OnMissionBehaviorInitialize`); the four callers of pool 2, the spin ranges, singular catalogue rows, the shipped notice, the register, the skills' log forms, the mutation attribution, the co-op exclusion count (pre-existing, left one short by the Arthedain commit) | Doc drift | Each doc was updated for its own feature, not re-read against the code after the second pool and the fixes | Fixed |
| 28 | LOW | Process: a doc-fix script ran for real while the final-state lenses were still reading, because a heredoc patch that was meant to add its dry-run flag failed and the next command ran anyway | Shell chaining | A heredoc mangled the patch's backslashes (a known trap) and the commands were not chained with `&&` | Lesson "A dry run and the patch that adds it are chained with `&&`" (`lessons/build-tooling-workflow.md`). The edits were doc-only, but one (MEDIUM, from the critic) wrote the planned per-pool WARNING rule into `skeleton-buffer-guard.md` before the code had it; the fix round then made the code match |
| 29 | LOW | A survived throw in TAOM's own `OnMissionBehaviorInitialize` drops every TAOM behaviour registered after it (the skeleton watch, the behaviour dump, the career perks), not only the closer (critic) | Same as 20 | Finding 20's fix looked at the one behaviour with a visible symptom | Documented in `mission-start-guard.md`; isolating each registration is issue draft 11 (it rewrites about 30 lines other changes also edit) |
| 30 | LOW | VanillaTuning ships its own map-view release, on the cover and on by default; with both mods loaded both release the same view (critic) | Coexistence rule incomplete | The "detect its effect" rule was written for three of the four ports | Documented ("turn one off") and forwarded to yotthani with finding 19's mechanism; a detection was rejected because his patch can stay applied while his own toggle is off |
| 31 | LOW | Co-op classification: the skeleton guard was filed as instrumentation (it is presentation); the start guard, like the two crash-capture toggles it was filed with, fits none of the four exclusion reasons (critic) | Classification doctrine | The new toggles followed the crash-capture precedent | The guard moved to presentation (no count changes); the three crash-containment toggles are the maintainer's doctrine decision (issue draft 12) |
| 32 | LOW | A test that could not fail: the executed pool-2 block's "pool 1 untouched" check read its snapshot after both blocks were written, on an execute-read page (critic) | Test design | Written as a companion to the executed-block test without asking what failure it could show | Deleted after a mutation showed it stayed green while the layout test failed |
| 33 | LOW | Always-loaded guidance (the orientation trap row, the Harmony path rule) and two settings catalogues still described the pre-guard behaviour; both new notices said "battle" though they fire in every mission; a lesson recurrence sat under the wrong entry (critic) | Doc drift | The first round updated the feature's own docs and the catalogues it knew of | Fixed; the notices were reworded before their translation run |

### Root-cause patterns, second round

**A port inherits upstream's timing.** Finding 19 is finding 1's pattern again: the code was right about what to call
and wrong about when, because when was copied. **A guard moves the failure, it does not remove it.** Findings 20 and 22:
code that turns a throw into survival, or a failed apply into a log line, must be re-traced for what the surviving
path now does. **Three repeats of the first round's lessons** (21, 25, 26) show that a lesson in a file the builder is
not told to read does not reach the builder; the rule line for 21 now loads with every patch file.

### Not applied, with reasons

- Rewording the shipped notice's line "TAOM.dll ... is original TAOM work" (re-review lens 4): declined, the
  maintainer's standing instruction is to leave that line as he set it.
- A first-byte prefilter in `ClipBudgetSignature.Find` (final lens 3, about 0.1 s per launch): pre-existing shared
  code this change did not modify; a follow-up, issue draft 10.
- Resetting the map-view cover count at a mission start (final lens 5): rejected by the simplicity criterion; the doc
  says the count runs for the whole launch.
- A guard for the other nine per-frame pools: waits for a measurement (issue draft 9).
- Isolating each of TAOM's hand-wired mission behaviour registrations (critic): its own change, issue draft 11.
- Counting `SurviveMissionStartFailures` as simulation-relevant (critic): it would split the three crash-containment
  toggles and move every peer's settings fingerprint; the doctrine is the maintainer's call, issue draft 12.
- Detecting VanillaTuning's map-view release (critic): his patch can stay applied with his own toggle off, so a
  stand-aside could leave no release at all; the doc tells players to turn one off.

## Codex review and convergence pass (2026-10-08)

Codex (gpt-6-astra at ultra, prompt `docs/reviews/codex-adversarial-yotthani-adoption-2026-10-08.prompt.md`, output in
`docs/reviews/raw/`) reviewed the fixed tree: 0 HIGH, 1 conditional MEDIUM, 2 LOW, all confirmed. It disputed four of six
known suspects with disassembly and decompile evidence, correctly. Two convergence lenses (data flow, engine
compatibility) ran on the same tree at the same time: 0 HIGH or MEDIUM, 0 incompatible, a set of LOW pins and wording.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 34 | MED (conditional) | A throw in the first mission's patch-application prelude, before the closer, still skipped it (Codex F1) | Same as 20 | Finding 20's fix moved the closer ahead of the registrations, not ahead of all of TAOM's fallible work | The closer is registered in a `finally` around all of TAOM's mission wiring; `BattleLoadCloserWiringTests` pins the try, the finally, the absence of a catch and that no fallible work stays outside |
| 35 | LOW | Registered first, the closer ticked last, so a TAOM behaviour that throws in every tick kept the window open (convergence, data flow) | A fix that traded one exposure for another | The move was judged by registration order alone; tick order follows it in reverse | The same fix: registered last, in the finally, it ticks first |
| 36 | LOW | A due map-view release survived a campaign change: Save/Load is a cover, and the loaded save's new map layer never matched the old one (convergence, data flow) | State that crosses a lifecycle boundary | The drop rule covered only "no campaign runs" | A new map's own layer drops an old map's due release, with a test |
| 37 | LOW | Pins and wording: the activation edge's order (`IsActive` set before the raise) was not pinned; `Priority.First` was said to put the prefix ahead of every foreign prefix; a comment placed a ready check in `OnResume`; eight test names credited the cover with the return's work; the cull's replay was called idempotent (a late failure ticks a few plates' notifications twice, once per launch); the skeleton doc filed both settings as instrumentation; the section 10 spin ranges used two notations (Codex F2, F3; both convergence lenses) | Wording and pins | Within normal review scope | Fixed |

Codex pass 2 (gpt-6-astra at high, on the fixes above; prompt `...-pass2.prompt.md`): 0 CRITICAL, HIGH or MEDIUM, 2 LOW,
both confirmed.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 38 | LOW | With the guard off, a throw in the patch prelude and a successful reload of the same Mission left two closers, so `BattlePlayable` was stamped twice (the repeated cleanup is harmless) | A fix that met a reload path | The `finally` design was checked for a single attempt; the engine reuses the Mission object on a reload | The `finally` adds the closer only when the mission has none; the pin checks it, with a mutation test |
| 39 | LOW | The BattleLoad bracket no longer stamps or counts the closer, and its doc row said "each stamped by name" | Doc drift | The move out of `AddTaomBehavior` changed a diagnostic the doc describes | Documented in `battle-load-diagnostics.md` |

## Decisions owed to the maintainer

1. A second engine-code guard for the second pool, or watch first and decide from the peak lines. **Decided
   2026-10-08: guard it now** (a 42-byte code block of TAOM's own design at `0x6AF50`, the same shape as the first).
2. The nameplate cull's default before its A/B: a persisted MCM default cannot be flipped later without renaming
   the setting (`docs/features/mcm.md`). **Decided 2026-10-08: ON now**, with the maintainer's A/B before the next
   release; until a release ships it, only his machine holds a saved value, so the default can still flip.
3. The other nine unbounded per-frame pools: documented, not guarded, decided in this session under the
   maintainer's "full control" instruction (2026-10-08); a guard waits for the measurement in issue draft 9.
