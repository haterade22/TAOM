# RCA: Fords of War (architect checks and /deep-review, 2026-10-30 and 2026-10-31)

## Top-line

Fords of War is a new campaign-free lane-push mission: about 240 new or changed files, built overnight
in a worktree in eight test-first stages. It went through three review passes before any commit:

1. Two architect checks during the build (engine layer, then the whole feature) and an adversarial
   verify of their fixes: 7 defects (2 medium, 5 low), 1 gap, and one wrong default found by the
   orchestrator (hold distance inside tower range).
2. `/deep-review` at tier 1: Lens 5 Data flow (split in two by area), Lens 2 Engine compatibility,
   Lens 7 XML, and one Step 2c expansion over the TAOM behaviours that attach to every mission.
   0 CRITICAL, 0 HIGH, 5 MEDIUM, about 20 LOW, and 5 wrong engine claims in comments and docs.
3. Codex did not run. Tier 1 calls for it, but paid dispatch needs the maintainer's word for this
   work, and he was asleep. It is owed before the merge.

Every confirmed finding below is fixed in the worktree, except the two marked as decisions. The last
verified run (2026-10-31): `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` with 0
errors; `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` with 30,216 passed, 0 failed
and 7 skipped (all pre-existing skips); `python tools/validate_moduledata.py` with 0 errors;
`python tools/lint_docs.py --fail-on-drift` with exit 0. Nothing ran in game: each runtime fix has an
owed check in [fords-of-war.md](../features/fords-of-war.md). A last convergence pass on the final fix-up
found no runtime regression and corrected three sentences (listed after the re-review table). Nothing
is committed.

## Findings

### Architect checks during the build

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| P1 | MED | A lane on Hold past its hold point got a new Move order every tick: `HoldPoint` returned the live formation centre and the director deduplicated with exact float equality. | Unstable target under an equality dedupe | The domain stage tested the rule's value, not its stability across ticks; the director stage tested "apply on change" with fixed fakes. | Lesson: a command deduplicated by equality needs a target that does not drift with the thing it moves. Fixed: Hold has one target, the hold line. |
| P2 | MED | A failed hero spawn left the hero Alive forever: gold and slot lost. | Failure path not reported upstream | The director logged and dropped a failed request; no stage owned "tell the match". | Fixed: `FordsMatch.OnSpawnFailed` refunds and frees the slot. |
| P3 | LOW | The gunner quiver refill wrote managed `MissionEquipment` only. | Engine effect inferred from a member name | Same pattern as D2 below. | Fixed by deleting it; `AmmoSupplyLogic` already refills natively. |
| P4 | LOW | The Nine projected no dread in a CustomGame (hero axis read only `CharacterObject`). | New context for an existing gate | The spec asked only that the gate admit the mission. | Fixed: the character id outside a campaign, as SignatureStrikes already does. |
| P5 | LOW | Photo Mode could not be left (no photo-mode view in the list). | Vanilla menu item with no handler in a custom view list | The view list was cut from a template without asking what each kept menu item needs. | Fixed: the camera view refuses Photo Mode. |
| P6 | LOW | The panel layer (100) sat above the escape menu (50). | Layer order chosen without the engine's table | The client stage copied an order from a sibling view. | Fixed: order 20, between the HUD and the menu. |
| P7 | LOW | `FordsCommandView` was 162 lines (ADR-002). | No line-count test | Fords had no `LineCount` test. | Fixed, and `FordsEntryPointLineCountTests` now pins every Fords entry point. |
| P8 | GAP | The escape menu's Options entry did nothing (no `MissionOptionsComponent`). | Conditional view with an unmet condition | The view list guarded the handler correctly, but the launcher never met the guard. | Fixed: the launcher adds the component. |
| P9 | MED | The hold distance (35 m) was inside the tower fire range (45 m), so a lane gathering at its hold line was under volleys. | Related defaults chosen independently | The spec listed each number with its own reason and never stated the relation between them. | Fixed: 60 m. Gate: `ShippedMatch_LaneHoldLineIsOutsideTowerFireRange`. |

### /deep-review

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| D1 | MED | A tower took damage from its own side (gunners behind it, defending archers, an inner tower's volley): the hit hook passed only the attacker's role, and `TowerDamageRules` charged any blow at least 1, which also stopped regeneration. | Identity attribute dropped at a layer boundary | The spec defined the damage table by role and never asked whose blow it was; the engine checks and binding tests verified `OnHit`'s signature, not who can call it. Lens 5 found it by tracing the blow from the engine. | Lesson below. Fixed: `TowerHit` carries the attacker's side; an own-side blow does 0. |
| D2 | MED | Tower volleys did almost no damage: `Mission.AddCustomMissile` passes a damage bonus of 0, so a missile carries only its item's own damage (`bodkin_arrows_a`: thrust 3). | Engine effect inferred from a member name (repeat pattern) | Every check verified that `AddCustomMissile` exists with the right signature. Nobody read its body for the damage it delivers. The existing lesson covers COST inferred from a name, not EFFECT. | Lesson below, plus a Lens 2 line (separate harness change). Fixed: no plain XML item has the right damage, so volleys fire a crafted javelin (`western_javelin_2_t3`), which is fired the way an agent throws one. Its native damage is owed in game. |
| D3 | MED | Capture-point state was built every view refresh and read by nothing: no panel row, no AI rule. | Declared output with no consumer | The spec's panel list (section 6) was written separately from the view (section 3) and left the points out. | Lesson below. Fixed: a read-only capture row. AI steering to points stays a later phase. |
| D4 | MED | A raging warg rider lost its lane formation for good: the mount's `OnAgentBuild` runs before the rider's build sets `Formation`, so the warg tree stored null, and the end of the rage wrote it back. Pre-existing; Fords makes it the normal path, because every Fords warg spawns after the first scan. | New firing context for existing code | Per-file lenses review the diff. Only the Step 2c sweep of every-mission behaviours in the new mission's context could see it. | Fixed in two steps: `PrepareOnFirstCall` stores the live formation (D4), then `ResetAiControlledAgent` writes only a non-null one (R1). Skill change below: a new mission type or spawn path gets that sweep by default. |
| D5 | MED | Each unit offer stored its price three times (`price`, `level`, `eliteWeight`); runtime read only `price`, and a test locked the three together. | Derived value stored beside its inputs | The config stage wanted the price visible in the JSON and pinned the derivation with a test instead of choosing one source. | Fixed: prices derive from level and weight; `price` is an optional override. |
| D6 | LOW | The navmesh guard in `Move` never fired: `WorldPosition.IsValid` does not test the navmesh. | Engine effect inferred from a member name | As D2. | Fixed: the guard uses `GetNavigationMeshForPosition`. |
| D7 | LOW | A fallen tower ran inside the native hit callback when the hit was on the main thread: `RunOrDefer` runs at once there. The registry said it was parked. | Thread helper semantics assumed | The registry text described the off-thread case only. | Fixed: the tower site always queues for the next tick. |
| D8 | LOW | NaN positions could count at every capture point (inverted gate polarity) and reach `AddCustomMissile`. | NaN polarity | The sibling gate used the safe form; the capture gate was written separately. | Fixed at the source (adapter filters) and in both gates. |
| D9 | LOW | A lane with no towers targeted a keep that stayed locked. | Two rules disagree on an edge case | `NextTargetOf` and `OwnerLostALane` were written in different stages. | Fixed: a towerless lane counts as lost. |
| D10 | LOW | Panel hero buttons enabled from gold alone; the match also needs a free slot and a running respawn timer. | Consumer re-derives the domain's verdict from a subset of its inputs | The VM computed its own `CanAct`. | Lesson below. Fixed: the view carries `CanBuyback`; the panel uses it and the slot count. |
| D11 | LOW | `EntityOf` returned destroyed entities, so the `AttackTower` fallback was unreachable. | Comment and code disagree | | Fixed. |
| D12 | LOW | Prefab flags `DestroyedByStoneOnly` and `DestroyOnAnyHit` act after the prefix and could split the mirror from the entity. | Engine applies state after the hook | Shipped data is clean; only another prefab or a tagged scene could trip it. | Fixed: both flags cleared on spawn. |
| D13 | LOW | Lane units spawned with no banner. | Origin default | `BasicBattleAgentOrigin.Banner` is null; the gunners already set it. | Fixed. |
| D14 | LOW | Load failures after the load started returned to the menu silently; every automatic exit lived in the panel view. | Single point of failure in the UI | The client stage owned all exits. | Fixed: exits move to core, in mission time; the failure notice shows. |
| D15 | LOW | Hero and lane row VMs kept the identity they were created with while the source list could shrink. | Row identity bound by position | Fixed for heroes in the fix-up, then the lane rows. | Lesson below. |
| D16 | LOW | Troop names showed the Custom Battle sort tag ("[AMordor] Hill Troll"); offers 9 and 10 had no hotkey; `maps.json` used `layout` for two things; two launcher behaviours did nothing; dead code in `FordsEngine.Character`; `FordsSession.HumanSlot` had no reader; stale comments in DreadMissionGate, DevConsoleGuard, dread-aura.md; no `patch-targets.md` row. | Polish and drift | | Fixed. |
| D17 | DOC | Five engine claims were wrong: "the engine closes a finished mission after 30 s"; the control model citing `AddTeamAI`; "Gauntlet listens to the typed events, not the plain one"; "spring, noon" as vanilla defaults; "parked for the next tick". | Engine claim written without reading the body | The architect checks verified members, not sentences. Lens 2 item 4 caught all five. | Corrected. |

### Re-review of the fixes (Lens 5 and Lens 2 on the fix diff)

A fix needs a fresh review. Lens 5 and Lens 2 re-read the 82-file fix diff against a pre-fix copy.
11 of the 12 data-flow fixes closed their findings and 31 engine members verified; these remained:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | MED | D4's fix closed only the rage path: the warg tree still stored a null formation, and `OnWargDied` wrote it into the rider whenever the warg died. | Partial fix of a shared value | The brief named the rage path from the 2c report; nobody traced every writer of the stored value. | The skill's fix-loop rule already says to check every caller at the outermost gate; it applies to every WRITER of a shared value too. Fixed: `ResetAiControlledAgent` writes only a non-null formation. |
| R2 | LOW | The new capture row read "taking it" while progress decayed. | View built from a field that means two things | `Capturer` stays set during decay; the view had no direction field. | Fixed: the point view says whether progress rose. |
| R3 | LOW | A tower tagged on a lane the scene does not have now opened the enemy keep (D9's rule counts its lane as lost). | A fix widened what bad input can do | The tag reader never bounded tower lanes; D9 made that matter. | Fixed at the source: the reader rejects the tag. |
| R4 | LOW | The AI broke price ties by deck order, so the hotkey reorder (D16) changed a Gondor purchase. | Hidden order dependence | The spec said the AI buys by role and price; the stable sort made position a tie-break. | Fixed: ties break by troop id. |
| R5 | LOW | Buy buttons were enabled during the countdown, when the match refuses them. | Consumer re-derives a verdict (pattern 2) | | Fixed: buttons follow the match phase. |
| R6 | LOW | D6 made the navmesh guard real, and a refused Move was never retried, because the director recorded the command before the call. | A fix turned a permissive adapter into a refusing one | The caller assumed every Move succeeds; that was true until D6. | Fixed: Move returns whether it ran; a refused Move falls back to the nearest checked waypoint, and only a Move that ran is recorded. |
| R7 | DOC | Four statements were wrong: dread-aura.md named `AgentMoraleInteractionLogic` as what turns panic into a rout (it is `MissionAgentPanicHandler`); the javelin damage sentence left out armour, the hit bone and the Throwing multiplier; the exit comment called an `async void` call throw-safe; "three callers" listed two. | Engine claim written without reading the body (pattern 1) | The dread sentence came from the orchestrator's fix brief, which relayed Lens 5b's mechanism without opening the engine: the `evidence-over-claims.md` §A.4 failure, spot-check a subagent's load-bearing claim before relaying it. | Corrected. The lesson below covers claims relayed from a reviewer as well as claims written fresh. |

The convergence pass (one reviewer, both lenses, on the last fix-up only) closed R1 to R6 and corrected
three sentences: the fallback comment and spec line that said a refused Move cannot recur (it can, for a
lane end the fitter keeps off the navmesh, with the same result as before the fix); the javelin damage
caveat, which said the hit bone and the impact speed only lower the damage (a missile on a human head or
neck does double, and a faster relative impact does more); and "panics only below 0.01 morale" (a
riderless mount also panics when hit). The skill allows one convergence pass, so the review stopped there.

Not fixed, by decision: the Dread drain cannot rout troops in a Fords match. The mode leaves out
`MissionAgentPanicHandler` on purpose (routing units would leave their lanes), and the aura's floor of
10 never reaches the engine's 0.01 panic threshold anyway. That is a design question
for the maintainer, now written in `dread-aura.md`. The camp feed keys stay: neutral camps are the
maintainer's decision 8, wired in a later phase.

Follow-up closed 2026-11-04 (88b189e80): AI steering to capture points (no issue: creating an issue needs the maintainer's word).

### Found by /verify before the commit

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| V1 | LOW | The 81 text keys were `FOW_*`. `ShippedModuleStringsPinTests` (`tools/tests/test_english_only_writers.py`) accepts only `taom_`, `TAOM_` or `aom_` keys, because the translator tells TAOM's own keys from vanilla overrides by key shape. A `FOW_` key is not vanilla-shaped, so the translator would still have translated it; the gate guards the convention. | House convention held by a Python gate only | Every verification run during the build and the review was `dotnet test`, reported as the full suite. The Python tool tests, which hold this gate, first ran in `/verify` Step 2b before the commit. Lens 7 checks XML references, not key names. | Renamed to `TAOM_FOW_*` in code, data, tests, the 13 strings files and the docs (1,280 replacements in 32 files). Lesson in `build-tooling-workflow.md`: run the whole `/verify` before a review closes. |

## Root-cause patterns

**1. An engine method's effect inferred from its name (D2, D6, P3, D17; repeat pattern).** The
existing lesson says an engine method's COST is a decompile question. Tonight the same failure hit
EFFECT: `AddCustomMissile` "adds a missile" but delivers none of a launcher's damage;
`WorldPosition.IsValid` "validates" but does not test the navmesh; `MissionEquipment.SetAmountOfSlot`
"sets ammo" but never reaches the native agent; `RunOrDefer` "defers" except on the main thread. Every
one passed a binding test, because a binding test pins existence. Two architect checks verified the
same members and missed it too, because they were asked to verify signatures.

**2. A consumer re-derives a decision, or drops an attribute, the domain already holds (D1, D10, D15,
D3).** The hit hook rebuilt the blow from a role and lost the side; the panel rebuilt `CanAct` from
gold and lost the slot and timer rules; row VMs rebuilt identity from a creation-time index; the
view built capture state nobody asked for. In each case the domain had the full answer and a layer
outside it computed a partial copy.

**3. A new mission context reaches old code (D4, P4).** A mode that spawns everything after the
first second and never has a main agent exercises paths no campaign or Custom Battle used. The
per-file lenses cannot see this by design; the Step 2c sweep found it.

## Why each check missed what it missed

- **Architect engine and final checks:** asked to verify spec conformance and engine signatures,
  they did, and found P1 to P8. They did not trace a blow from the engine to the rules (D1), read the
  body of `AddCustomMissile` (D2), or run the every-mission sweep (D4).
- **Adversarial verify of the fixes:** scoped to the fixes, as designed; it found the hero-row bug
  the refund fix exposed.
- **Lens 5a (match):** found D1, D3, D8, D9, D10, D11, D12. It does not audit engine semantics.
- **Lens 5b (shell):** found D10, D14, D15 and the stale comments; it judged the Dread drain a design
  question, correctly.
- **Lens 2:** found D2, D6, D7, D13, the launcher redundancy and all five claims in D17. It audited
  vanilla behaviours only, by scope.
- **Lens 7:** found D5 and the data polish; every gate passed.
- **Step 2c expansion:** found D4; the other 45 behaviours stand down or run as intended.
- **Re-review (Lens 5 and Lens 2 on the fix diff):** found R1 to R7. R1, R3 and R6 are fixes that
  were correct where they were aimed and incomplete or widening elsewhere, which is the reason the
  skill re-reviews a fix in a tier 1 area.

## Preventive actions

1. **Lesson** (`lessons/adapters-taleworlds-api.md`): an engine method's EFFECT is a decompile
   question, never an inference from its name; quote the body line that produces the effect an
   adapter relies on.
2. **Lesson** (`lessons/gamemodels-services.md`): expose the domain's verdict to every consumer; a
   hook, a view model or an AI never re-derives it from a subset of the domain's inputs.
3. **Gate (done):** `ShippedFordsConfigTests.ShippedMatch_LaneHoldLineIsOutsideTowerFireRange` fails
   when the shipped or the built-in hold distance is not greater than the tower fire range (checked by
   setting each to 40 and restoring it).
4. **Skill change (separate commit):** `/deep-review` Step 2c gets one bullet: a change that adds a
   mission type or a spawn path launches one agent on the behaviours and default views that attach
   to every mission, in the new context. Lens 2 item 2 gets one line: for every engine call whose
   effect the feature relies on, quote the line that produces that effect.

No feedback memory: every rule above lives in the repo.

## Addendum: the self-diagnosing logging change (2026-10-31)

After the merge (47bf2fdc9), Mike asked for logging that judges each in-game check by itself. The change
adds `Main/Features/FordsOfWar/Diagnostics/` (a run report and 16 verdict rules), the engine seams that feed
it, `Main/Features/Warg/WargRageLog.cs` and TAOM.Client seams. It was designed by an architect, attacked by a
critic, revised, built in four Sonnet stages, checked for conformance by an architect, then reviewed by
`/deep-review` at tier 1 in two waves plus a Step 2b adversarial review and a Step 2c root-cause agent. Codex
did not run (a paid call; no word from Mike for this change).

### Conformance check during the build

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| K1 | MED | A prompt fault after an earlier run was charged to that closed run. | Wrong owner of late evidence | The builders tested each member alone, not a second run. | Fixed; scenario S5 drives the session tail. |
| K2 | MED | A console start that failed on route C wrote its final pass before its console record existed. | Order of producers | Tests called the members in an order the engine never uses. | Fixed; the harness calls in the engine's order. |
| K3 | LOW | Four small items: the frame histogram was not reset per run, the warg log kept the ended mission, one string per volley hit, check 1 let a newer good flow hide a fault. | Per-run state and cost | Checked against the design only. | Fixed in wave 1. |

### Wave 1 (Lens 2 twice, Lens 5 twice)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| L1 | HIGH | Check 7 read a hero's lane from its spawn position, but every generated lane starts at the same base point and `NearestLane` gives ties to lane 0, so correct lane 1 and 2 spawns FAILed. | Geometry assumed, never run through the real generator | The critique proposed the position check (text against text); the test used a layout with parallel lanes 50 m apart. | Fixed; the wave 2 fix then deleted the comparison as a tautology (row N6). Harness scenario S1 runs the real generator. |
| L2 | MED | The warg return time was wall-clock, judged on mission time: a pause of 60 s failed a working return. | Clock mismatch | Hand-set timestamps in tests. | Fixed (mission clock); scenario S3 pauses. |
| L3 | MED | A rage whose reset never ran (rider killed or thrown off a living warg) stayed open forever: check 12 could PASS while a rider lost his formation, and tracking stopped at 100 open episodes. | Producer path the rule could not see | Counts were hand-set; nobody traced every exit of an episode. | Fixed: `stranded` after two looks in a row, and `riderInactive`. (A separate slip, a second rage that dropped a waiting episode, is row N6's `superseded`.) |
| L4 | MED | Check 4 `inside` FAILed a camera parked on the boundary: the clamp puts it exactly on the edge and the native inside test is strict. | Engine semantics against our own clamp | Probe tests covered flags only. | Fixed: outside only beyond 0.5 m of the edge; scenario S4. |
| L5 | MED | A match started from the menu that failed on route B or C produced no FAIL in any row. | Missing FAIL branch | No test judged a failed start. | Fixed: check 3 `started`. |
| L6 | LOW | 16 smaller items: a dropped tower still PASSed; the hero link had no side filter; the hold settle used a fixed 10 s; the Patch131 note lost `ranged`; a warg count looked up offer ids in a troop-id table; a route C run got a stale final pass; a dead `cancel` branch; a warning stamped with the old run; check 15 had no close bound; late session evidence was never judged; `installed` was recorded before the option existed; two weak test pins; an optional parameter; a wrong tick-order comment; shielded hits in medians; paused frames counted; three doc errors. | Mixed | Mostly hand-built evidence. | All fixed in wave 1. |

### Wave 2 (Lens 1, 3, 4, 6; Step 2b; Step 2c)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| N1 | CRIT | `WargRageLog` held the sealed `Agent` and `Formation` in a class with decision logic (ADR-007), which forced its tests into `RequiresGame`. | ADR-007 | Wave 1 runs Lens 2 and 5 only; the standards lens runs in wave 2 by design. | Fixed: object handles, slot check, a reflection gate that fails on any TaleWorlds type in its signatures. |
| N2 | MED | The camera probe got the report through a swallowed `IoC.Resolve`, so no unit test reached the camera evidence. | Locator in a helper | Builders copied a view pattern into a non-boundary class. | Fixed: constructor injection; probe tests added. |
| N3 | MED | The doc said to read the last final pass, but its own steps need three runs. | Doc against procedure | Written per run, read per session. | Fixed in the doc. |
| N4 | MED | Check 16 judged TAOM's config cap, not the engine agent limit, while the doc said it judged the agent count. | Rule against its claim | Nobody compared the rule with the doc sentence. | Fixed: an `agents` part against the engine limit. |
| N5 | MED | Check 11 could not FAIL when the towers never fired or no hit was read. | Missing FAIL branch | Only the happy path was tested. | Fixed: two FAIL rules; `Fire` returns the missiles it added. |
| N6 | MED | Five more false verdicts from the root-cause read: check 15 exit in wall time; the check 7 lane comparison was a tautology after L1; the check 8 front rank spreads across the lane; the check 12 return ignored formation depth; a second rage dropped a waiting episode. | Producer-made evidence never judged | See the pattern below. | Fixed in wave 2. |
| N7 | LOW | 13 smaller items from Lens 1, 3 and 4 (a refused order logged every tick, a double lock, bare layout numbers, file names, comments, a purity gate that let three forms of `TaleWorlds` through, an inert sleep, a notice part that could only pass, a human comparison a test can make, missing telemetry and `Ended` tests, a hold FAIL that named no tower). | Mixed | | Fixed in wave 2. |

Lens 6 proposed 12 improvements, all applied (four behaviour-changing ones with Mike's approval): reuse
`MissionPerf.FrameStats`, one tower damage reason enum, the blow counts as a `[class, reason]` grid, one run
clock, first spawn per lane, one `Ask` helper, and six smaller items (warg counts as one evidence type, an unread
field, one reply constant, one late-pass helper, one start rule, and an accurate volley log comment).

### Root-cause pattern

**No verdict rule was ever judged on evidence its real producers made, in the engine's call order, with its
clocks and geometry.** Stage S1 built and tested all 16 rules before any producer existed, on hand-built
records, and wrote those tests after the code. The critique reviewed text against text: two of its fixes
(the lane from position, the strict inside test) became false FAILs. This is the fourth time the category has
shipped (testing-qa lessons #960, #925 and #1037 were fakes of adapters; here the hand-built object was the
feature's own record, so none of them applied).

### Preventive actions

1. **Harness (done):** a test-side scenario harness drives the real report, director, generator, placement,
   probe, exit clock and camera maths, with faithful fakes, in the engine's per-frame order, then judges. Five
   scenarios (clean match, one fault per row, pause and hitch, camera edge, session tail) and a contract
   table that ties every verdict part to the scenario that fails it, or marks it pass-only.
2. **Lesson** (`lessons/testing-qa.md`): judge a self-check rule only on evidence its real producers made.
3. **Lens 4 check 9** gains one clause for self-check verdicts.

Follow-up closed 2026-11-04 (17b071c7a, 7a21239dc, 4a7b7164d; the in-game check is owed): a warg rider who leaves a living warg mid-rage never gets his formation back; check 12 now reports it as `stranded` (no issue: creating an issue needs the maintainer's word).
Follow-up closed 2026-11-04 (Mike: leave it, check 8 names the tower if it happens): after a lane loses its towers, its hold point before the keep can sit inside a neighbouring lane's inner tower range on maps with 300 to 500 m between the bases; check 8 now names the tower (no issue: creating an issue needs the maintainer's word).
Follow-up closed 2026-11-04 (9ea4ffd44): the AI commander rebuilds its full read model every tick and uses it once every 5 to 12 s (pre-existing; no issue: creating an issue needs the maintainer's word).
FOLLOW-UP: the Fords verdict rows move onto the shared `[Check]` line, whose API (#1071) landed on trunk while this change was in review (#1069).

UNVERIFIED until the first game run: whether the engine's `MissileTotalDamage` equals the volley item's thrust
damage (check 11 FAILs with both numbers if not), and whether a missing keep prefab provokes a route A start
failure for item 15.

## Found in game (2026-10-31, first match)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| G1 | HIGH | The mission start ran twice. `FordsOfWarMissionLogic.AfterStart` spawned the tower gunners inside the engine's `foreach` over the mission behaviors; DualWield's `SpawnAgent` postfix added its behavior on that first spawn (the mode was already Battle, set by `MissionCombatantsLogic.AfterStart`), so `MoveNext` threw outside every call the start guard wraps, and the engine loaded the mission again on the same mission object. the initialize and early-start calls ran twice, and `AfterStart` ran twice for the behaviors up to the one that spawned: towers and gunners were built twice, TAOM behaviors were added twice (creatures got "a tree ... already exists"), the combatants logic and the boundary placer failed ("Number of teams is not 0", "An element with the same name already exists"), and the scene showed black. Proof: TAOM log line 489 and the behavior list (DualWield at index 70, added once, between the two passes); DualWield's own log, `ADDED (mode=Battle)` at 15:39:37.614 between `OnMissionBehaviorInitialize` mode StartUp and mode Battle. | Spawning inside an engine loop | Every review read TAOM code only; no lens asked what the other installed modules (DualWield is in the load order) hook on the start path, and the engine doc warned about throws in `AfterStart`, not about mutating the list it walks. | Fixed: `AfterStart` marks the start as due and the first `OnMissionTick` starts the match (test `AfterStart_StartsNothing_TheFirstTickStartsTheMatchOnce`). The rule is in `mission-and-missionbehavior-lifecycle.md` ("Never spawn an agent inside `AfterStart`") and the orientation trap index. |
| G2 | HIGH | The scene rendered black under the command panel. Three panel boxes were `CoverChildren` with a `StretchToParent` background child; Gauntlet measures each child against the parent's whole space (`DefaultLayout.MeasureChildren`), so each box grew to the full screen and its 63 to 69 % black stacked over the scene (about 4 % left); the bottom deck drew from the top left. Proof: the camera pose lines put the camera 47.6 m above the terrain looking 46 degrees down, the scene reported ReadyToRender, and the fixed prefab shows the battlefield. | Repeat offender (third time: `lessons/localization-ui.md` lines 139 (#388) and 1221 (#866)) | The lesson existed but no gate did; the prefab review checked bindings and events, not sizing. | Fixed: each box paints its own background, with positive margins inside. `FordsCommandPrefabTests` pins the Fords prefab, and `GauntletStretchInCoverRatchetTests` pins every prefab file under `Main/_Module/GUI` (five older files are counted and may only fall; grid parents are exempt; proven RED on the old Fords prefab). |

Resolved overnight (see the next addendum): AI melee now strikes towers (check 9 PASS in every unattended match;
731 native tower blows in the first 600 s run), and the layout fitter plus Rohan Plains II as the default scene
remove the bad fit (0 points dropped, 14 of 14 towers built).
Follow-up closed 2026-11-04 (Mike: StagedSiegeHud and CareerScreen fixed in 95a348a60, the other three on their next edit, and the ratchet holds them): five older TAOM prefabs hold the stretch-in-cover pattern (MomentumView, CareerScreen, BattleActionBar, PreBuildCharacterSelection, StagedSiegeHud); triage each, then lower its count in the ratchet (no issue: outside this feature, and the maintainer decides the work).

## Addendum: check 9, unattended runs and the second review (2026-10-31 night)

Mike asked for the work to continue overnight with full authority. A research workflow (four readers and a
critic) found that the 16:54 match could not prove "no AttackTower was ever given": the lane-command log stops
after 30 lines and the report kept only the last command per lane. It found three causes that each explain
"units just sitting": the lane rules measured from the mean of every unit, which each new wave pulled back to
the base; every lane unit spawned with `isAlarmed=false` (vanilla battles spawn alarmed); and the fitter dropped
middle-lane points and three towers on Rohan Plains I, which left `keep_west` open. Four builders then added an
env-armed autorun with a launcher (learned from ArmorySim's direct launch, not integrated), diagnostics for every
link of the melee-on-tower chain, a lead-group lane rule and a layout fitter with a retry planner. Four
unattended matches ran on the desktop with nobody at the keyboard.

| Run | Setup | Result |
|---|---|---|
| `E:/Temp/fords-runs/20261031-215310` | first launch | stalled 10 min on the engine's "Mod change detected" notice (row U1) |
| `20261031-220826` | 600 s, East AI against a passive West | 5 PASS, 0 FAIL; check 9 PASS (731 native tower blows, AttackTower given 7 times) |
| `20261031-222418` | 1800 s, AI on both sides (`west_ai`) | 7 PASS, 0 FAIL; checks 7 and 16 PASS; no tower fell |
| `20261031-225939` | 1800 s, both AI, deployed `match.json` lowered to 1200/1600/2400 HP and no regeneration | 7 PASS, 0 FAIL; no tower fell; the run could not prove which numbers loaded (row R3) |
| `20261101-075050` | 900 s, both AI, the fixed build | 6 PASS, 0 FAIL; check 3 `exceptions` and check 9 `bypass` PASS; single-unit leads fell from 325 of 2155 lines to 14 of 1074; lowest tower HP 6781 of 8000, so no damage stage was reached |
| `20261101-081305` | 1500 s, both AI, the fixed build, deployed `match.json` lowered to 2000/2400/3200 HP with regeneration on (logged by the `[Tower] config` line) | 6 PASS, 0 FAIL, 0 swallowed exceptions; `tower_west_0_0` fell to 1195 HP (below the 1500 HP stage line) and regeneration healed 1276 HP in total, so it was struck again after healing: the row R2 sequence, now without the exception |

### Found by the unattended runs

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| U1 | MED | The first unattended start stalled before the graphics device: the engine shows a blocking "Mod change detected" notice (one OK button) whenever the shader cache was built for another module list. | Engine UI on the unattended path | The autorun research read ArmorySim's shader-list guard as a time cost only ("not a blocking dialog"); nobody looked for dialogs owned by the game process. | Fixed: the launcher clicks OK on exactly that notice, fails on a crash-titled dialog and logs any other dialog once. |
| U2 | MED | The 600 s run threw `IndexOutOfRangeException` inside the engine (row R2) and still ended `fail=0`; the launcher printed PASS. | Self-check blind to swallowed exceptions | No verdict and no launcher rule read TAOM's CrashReport output. | Row R4. |

### /deep-review of the batch (wave 1: Lens 5 twice, Lens 2, Tooling; wave 2: Lens 1, 3, 4, 6)

Codex did not run (a paid call; "full authority" did not name it). Every finding below was re-read against the
code or the engine decompile before it entered this table.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | HIGH | `LaneGeometry.LeadGroup` took the single most advanced unit as the front, so one runaway unit led a 44-unit lane past a standing tower: 32 % of East samples had the lead more than 20 m past the next standing tower, 303 of 2172 lines had `leadCount=1`. | A group statistic from one extreme sample | The builder's tests used clustered formations; no test put a lone unit ahead of its lane. | Fix: the front is the k-th most advanced unit (k = min(n, max(3, ceil(n/10)))), the lead group lies within the band on both sides of it, and a lead level with or past the next standing tower attacks it. A march cap was rejected (it could park a lane 22 m short of attack range). |
| R2 | HIGH | Tower regeneration raised `DestructableComponent.HitPoint` over a damage-stage line; the next hit set stage 0 and the engine read `_destructionStates[-1]` after hiding the model and turning its physics off. | Engine state machine driven backward through a public setter | `FordsTowerAdapter.Heal` predates this batch; alarmed lanes made tower blows land for the first time, and no test or rule knew the setter drives a one-way state. Third case of the "engine effect inferred from a member name" pattern (D2, D6). | Fix: the adapter sets `OnCalculateDestructionStateIndex` so the shown stage never moves backward; a binding test pins the field. Engine note in `usable-machines.md`, one rule in `.claude/rules/adapters.md`, a lesson. A domain heal ceiling was rejected (it would have changed balance). |
| R3 | MED | The report kept no tower hit points, no damage totals and no effective tower config, so "no tower fell in 30 minutes" could not be split into too little damage, regeneration, or a config that did not load. | Diagnostics one link short | The diagnostics brief named the blow chain, not the hit-point chain. | Fix: lowest rules HP, damage per attacker class, regeneration healed and a melee raw-damage summary per tower; one line with the effective tower config at match start. |
| R4 | MED | A swallowed engine exception passed the self-check and the launcher (row U2). | Self-check blind to swallowed exceptions | See U2. | Fix: check 03 gains an `exceptions` part fed through the CrashReport seam; the launcher fails on a `[CrashReport]` block inside the run or on any FAIL row. |
| R5 | MED | Check 9's info counted "contact" near any standing tower (locked towers, the keep, fights with the garrison) and gunner hits from either side, so it could not name the broken link. | Diagnostics that count proximity, not cause | The counters were designed from the research text, before any run produced them. | Fix: a per-stream ladder keyed on the stream's own AttackTower target (given, flagged, at the target, tower blows against gunner blows, applied); a `bypass` part fails when a lane marches past a standing tower. |
| R6 | MED | `[LaneState] applied=` could never show the nearest-waypoint fallback it was documented to show. | Record of the request, not the effect | `_applied` is the change-detection key; the line reused it. | Fix: a separate sent-target record; one line format. |
| R7 | MED | Launcher: the dialog click had never run in a launcher run, preflight failed open when the process query failed, a creation-time check duplicated what the Popen handle guarantees, and exit 0 could hide a crash at quit. | Unattended harness trusting exit codes | Python written before its tests; the guard functions had no tests. | Fix: PostMessage click logged once, fail-closed preflight, kill through the Popen handle, crash scan after the done file; tests for every guard. |
| R8 | MED | `FordsTelemetryLogic` grew to 175 lines across a partial split, which the per-file ADR-002 gate cannot see. | Gate measures files, rule measures classes | The split was made to stay under the per-file gate. | Fix: the gate sums the files of a partial class; the class is back under 150. |
| R9 | MED | The autorun host resolved its services itself instead of the null-host pattern the module already uses. | Locator in a host | The builder believed a Client type could not be registered from Main. | Fix: null host in the module, the Client host in `TaomClientComposition`, the driver resolved only when armed. |
| R10 | MED | The doc's hold arithmetic was backwards in three places, and the doc did not reflect the two full matches. | Doc written before the evidence | The doc pass ran before the runs. | Fixed in the doc pass after the fixes. |
| R11 | LOW | 37 smaller items: off-navmesh counted twice per tick, a lane's last tower dropped by a failed build, camps scored by the planner though unused, a quoted `out=` threw, `west_ai` missing from the setup record, an unread `ArmPath`, `final_timeout` overwriting the outcome, an unbounded Live phase after an outside EndGame, a dead "not armed" branch, an autorun run parked for the console, an unguarded logger in a catch, contradictory limits accepted silently, six launcher guards, the module list copied by hand, two write guards, a silent catch, an invisible BOM literal, enum names as strings, disagreeing log helpers, comments and slot names, dead geometry helpers and overloads, test names, the lead stored on the engine snapshot, a duplicated flag, slide distance, the lead loop's cost (0.45 ms per tick at 240 units in the Debug build), list allocation, planner timing, a comment on gunner hits, and four missing tests. | Mixed | | Fixed in the fix pass (the list with the proof of each is in the commit). |

### Root-cause patterns

1. **A rule or a report built on a convenient sample.** R1 used the single furthest unit, R5 used any unit near
   any tower, R3 kept a difference instead of the values. Each was the easiest number to compute, and each was
   tested on clustered, hand-built positions. The real runs supplied the outliers.
2. **Engine state that only moves one way, written through a setter that allows both.** R2 repeats D2 and D6: a
   member's name (`HitPoint`) was trusted to describe all of its effects.
3. **An unattended harness that trusts the process and its own exit code.** U1, U2, R4 and R7: the first run
   stalled on a dialog nobody looked for, a swallowed engine exception passed, preflight failed open.

### Preventive actions

1. **Rule:** `.claude/rules/adapters.md` "A destructible's shown stage must only move forward", with the engine note in
   `docs/reference/engine/usable-machines.md`.
2. **Lessons:** `lessons/adapters-taleworlds-api.md` (R2), `lessons/testing-qa.md` (a self-check fails on a
   swallowed exception and keys its ladder on the causal target; test group statistics with a lone outlier).
3. **Gates:** the ADR-002 line gate sums partial classes (R8); check 03 `exceptions` and the launcher crash rule
   (R4); check 09 `bypass` (R1).
4. **Process:** an unattended match on the desktop is now the proof before a Fords merge
   (`python tools/run_fords_autorun.py --west-ai`), and its folder is cited in the commit.

Follow-up closed 2026-11-04 (22d4f4c70: on the hand-built scene; the four generated scenes are unmeasured): AI melee spends many swings on the invulnerable tower gunners (1,075 melee blows in the 600 s run); the maintainer decided on 2026-11-01 to build real tower structures on the map with unkillable bowmen up on the tower, which needs the scene work and tagged gunner spots the layout reader can read (no issue: the maintainer chose no GitHub issues for Fords of War).
Resolved (2026-11-01, next addendum): most melee blows on a tower did zero raw damage because vanilla scales a thrust on an object to almost nothing; a thrust damage floor now applies.
Follow-up closed 2026-11-04 (run 20261103-203523: check 14 passed): no tower fell in two 30-minute AI matches, so check 14 (keep, result, exit) is still unseen; tower balance is the maintainer's call, and the R3 evidence gives the numbers (no issue: the maintainer chose no GitHub issues for Fords of War).
Follow-up closed 2026-11-04 (c31a7ef69, 91185bdc5: the launcher tries procdump; untested on a real crash): a crash in an unattended run loses its dump, because the watchdog asks a human before it writes one and the launcher stops the game (no issue: the maintainer chose no GitHub issues for Fords of War).
Follow-up closed 2026-11-05 (no generated map ships since Mike kept only Fords of War): the layout planner's limits (10 % dropped, 5 % bad segments) are unmeasured; set them from the attempt lines of the four scenes (no issue: the maintainer chose no GitHub issues for Fords of War).
FOLLOW-UP: `FordsSnapshotBuilder` still groups with LINQ (about 36 KB per tick at 240 units); a bucketed builder brings it to about 4 KB (no issue: pre-existing code, the maintainer chose no GitHub issues for Fords of War).

## Addendum: Codex reviews and the zero-damage melee investigation (2026-11-01)

Mike authorized two Codex reviews (gpt-6-astra, `ultra`): review A on the night batch (`d4fb51a50..c2c3a6f28`) and
review B on the whole feature (`c2c3a6f28`), which had never had a Codex pass. He also asked for `/investigate` on
the zero-damage melee blows. Codex found five MEDIUM defects and no HIGH; every one was confirmed by a failing test
before its fix. The investigation found vanilla behaviour, and Mike chose a design answer.

### Root cause of the zero-damage melee blows

A probe on `MissionBehavior.OnRegisterBlow` classified every melee blow on a tower in the unattended match
`E:/Temp/fords-runs/20261101-095058`: 111 Cut swings all landed damage, and all 78 zero blows were Pierce thrusts that
did not hit with the tip (44 with a magnitude under 1, 34 with a magnitude of exactly 0, mostly riders and lancers at
the start of their animation). Vanilla v1.5.6 scales a melee blow on an object by `GetEntityDamageMultiplier`
(Pierce 0.1), a non-tip thrust by `NonTipThrustHitDamageMultiplier` (0.15), and truncates to an int
(`MissionCombatMechanicsHelper`, lines 204-205, 420-448, 691-695). No TAOM model can zero these blows (the damage
model runs only on a positive value). Decision (Mike, 2026-11-01): a thrust damage floor for Fords towers, a share
(`towers.thrustFloorFactor`, 0.2) of the weapon's thrust damage, through the normal tower rules.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | MEDIUM | The launcher passed a run that crashed at quit: the exit code was only logged, and the CrashReport scan stopped at the done time, which the driver writes before it quits. | Harness judges the done file, not the process | The tooling lens and the fix builder treated the done file as the end of the run. Codex's remedy ("fail on nonzero") was itself wrong: Bannerlord exits -1 after a normal quit. | Fixed in `8d883051e`: exit 0 and -1 pass, any other code fails, the scan runs to the end of the log. Lesson in `lessons/build-tooling-workflow.md`. |
| C2 | MEDIUM | A tower slide could put the outer tower behind the locked inner tower when tower fractions are close. | Invariant never checked | The fitter checked walkability per tower, never the order the chain depends on; no close-fraction test. | Fixed: a slide keeps chain order with 10 m separation (`FordsLayoutFitterTests`). |
| C3 | MEDIUM | Spawn backlog did not reserve lane capacity: a lane reached 91 of 90. | Two components, one invariant | The scheduler and the director each enforced the cap on their own view (live units) without the pending requests. | Fixed: the match reserves issued ordinary units until they spawn or fail (`FordsMatchLaneReserveTests`). Lesson in `lessons/testing-qa.md`. |
| C4 | MEDIUM | An exception during the match start left the director's report bindings and session alive. | Cleanup on the success path only | The logic shut down on a false return, not on a throw. | Fixed: `Start` shuts down on a throw and rethrows (`FordsMissionDirectorReportTests`). |
| C5 | MEDIUM | Check 06 passed on a gold command from an earlier match in the same process. | Session record read by a per-run check | Console records carry the parked run, normally 0. | Fixed: `FordsConsoleRecord.ActiveRun`, check 06 filters by it. |
| C6 | MEDIUM | The zero-damage measure counted kicks and own-side blows as RawZero, and `meleeRaw` mixed own-side blows into check 9. | Measurement contamination | The first probe was built from the research text before any run. | Fixed: weaponless and own-side blows are classified first and left out of `meleeRaw`. |
| C7 | MEDIUM | The probe's comment said it saw a blow before vanilla scaled it; `BaseMagnitude` is already scaled. | Engine effect inferred from a name | Written from the research summary. | Fixed comment and test row (Lens 2). |
| C8 | LOW | Nine smaller items from Lens 5 and Lens 2: a NaN magnitude bucket, a trace built before its cap, a count-only path through the damage method, a dead weaponless branch, probe counts on a fallen tower, stale comments, two missing tests, per-run trace caps, no mounted bit in the probe key. | Mixed | | Fixed in the same batch. |

### Re-review of the floor (Lens 5, Lens 2)

A tier 1 re-review of the floor and the C1-C8 fixes by the data-flow and engine-compatibility lenses. The proof run `E:/Temp/fords-runs/20261101-103348` (900 s, AI on both sides) floored 129 blows for 1,020 hit points, labelled none of its 264 melee blows RawZero (228 applied, 8 on locked towers, 28 from the tower's own side), and ended with 6 PASS and 0 FAIL verdicts. A second 900 s run on the fixed build (`20261101-112404`) floored 38 blows for 266 hit points, again left no melee blow RawZero, and its check 9 lock part passed on 13 enemy blows on locked towers, own-side blows now apart.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C9 | MEDIUM | Own-side blows on locked or fallen towers were labelled Locked or Destroyed, so check 9's lock part could pass on own-side blows alone (in run 103348, 34 of its 38 Locked blows came from the tower's own side, mostly its archers), and meleeRaw still held own-side blows (C6 was incomplete). | Classification order | The C6 fix put own side first only on the zero branch, and the precedence test used attackers with no side for the locked and destroyed rows. | Fixed: own side is the first label; TowerDamageRulesTests rows with an own-side attacker on a locked and on a fallen tower. Lesson in `lessons/testing-qa.md`. |
| C10 | LOW | The docs and comments said the floor reaches "a thrust that missed with the tip" and that the engine deals "0.15 x 0.1 of the weapon's damage". The floor reaches any zero melee blow from a weapon with thrust damage, and the factors multiply the blow's base magnitude. | Engine effect inferred from a summary (as C7) | Written from the research summary, not the decompile lines. | Fixed text, which cites the helper's lines. |
| C11 | LOW | No scenario ran a floored blow through check 9's drift rule (the scenario's thrust parameter had no caller). | Test gap | The floor's tests stopped at the rules and the patch. | Fixed: a floored-strike scenario. |
| C12 | LOW | The trace could not show whether one attack lands two contacts, each now floored, or which thread runs a melee hit. | Missing evidence | The probe was built to classify blows, not attacks. | Fixed: the TowerMelee trace carries the attacker's agent index and the managed thread id (`agent=<index> thr=<thread id>`); the second proof run `20261101-112404` answered both: all 76 traced melee blows ran on managed thread 39, the script-tick main thread, and no attacker struck the same tower twice within 0.6 s with rising attack progress (scope: the 76 traced blows; the trace is capped per tower). |

Open for the maintainer: the floor replaces only a zero, so a weak shaft thrust with raw 1 to 7 takes less than a floored zero (about 8): in run 103348, tower_west_0_0 took raw 1, 5 and 7 blows at face value beside 66 floored ones. The patch also floors the rare zero swing from a weapon with thrust damage (1 of 129), because `OnHit` carries no strike type. A max(raw, floor) rule would remove the inversion but also raise weak swings, which the maintainer's decision keeps as the engine computes them; a smaller factor (0.05 gives about 2 for a 40-thrust lance) is the other lever. Decided (Mike, 2026-11-01, on Claude's recommendation): keep the floor as built. The inversion touched 3 of 69 melee blows on the most-hit tower, a max(raw, floor) rule would raise swings against the decision, and a smaller factor would weaken spear assaults again.

Follow-up closed 2026-11-04 (Mike: no guard until a drift appears, check 9 reports one): `FordsMissionDirector.AdjustTowerDamage` calls the report's `TowerBlow` after the rules changed their hit points, with no fault guard; if the report throws, the patch keeps the engine's raw value, so a floored blow would take about 8 in the rules and 0 in the engine, which check 9's drift rule would flag (no issue: the maintainer chose no GitHub issues for Fords of War).

Codex disputed 57 of the suspects in its two prompts with code evidence; those are recorded in `docs/reviews/REVIEW-LOG.md`.

Follow-up closed 2026-11-04 (Mike: sort only when a scene author hits the warning): a tagged layout whose inner tower marker comes before its outer one can reject a layout that a different fitting order would keep; no shipped scene uses tagged layouts (no issue: hypothetical, the maintainer chose no GitHub issues for Fords of War).
## Addendum: tower balance and the lane siege rule (2026-11-01)

Mike asked for Claude's recommendation on tower balance ("Proceed with your recommendation"). Two independent analyses
of the run evidence and a reconciler set towers to 1200, 1600 and 2800 hit points with regeneration of 1 per second after
45 s. The 2700 s unattended match `E:/Temp/fords-runs/20261101-123312` still felled no tower: the busiest outer tower
took 815 hit points in 45 minutes and healed 659. Config alone could not make towers fall.

### Root cause: a lane at an enemy tower always charges

An architect diagnosed the stalemate from that run, and a second architect tried to refute the diagnosis.
`LaneMarchRules` checked the Charge branch (any enemy within 25 m of the lead) before the AttackTower branch, and
defenders spawn behind their tower and walk out through it, so a lead group at an enemy tower always had an enemy in
range. West received no AttackTower command in runs 081305, 103348, 112404 and 123312. In run 123312, lane 1, from
mt 485 to 565, West had 56 to 67 live units against East's 23 to 37, but at the tower it was about 13 against 18, and
every sample was Charge. The lane-wide ratio was the wrong measure; the challenger's local count at the tower was kept.

**Fix (the siege rule):** under Push, when the lead is within `towers.fireRange` of the lane's next enemy tower and the
units of the side's lanes that target that tower outnumber the tower owner's units within fire range of it by 1.5 to 1,
the lane attacks the tower; it keeps attacking while it is not outnumbered there and its lead stays within fire range
plus 15 m. The rule applies to the player's lanes as to the AI's, as the maintainer's design gives both sides the same
orders. The `[LaneState]` line carries `near=<attackers>/<defenders> siege=<true|false>`.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| S1 | HIGH | Siege entry accepted a lead past the tower at any distance (the lane-arc reach test), while staying needed 60 m, so the command alternated AttackTower and Charge every tick; the engine's 0.5 s AttackEntity timer restarts with each order, so no unit would ever be sent at the tower. | Entry and exit bands that disagree | Every siege test entered at 10 m, so no test reached the arc path into the state. | Fixed: entry implies stay. Lesson in `lessons/testing-qa.md`. |
| S2 | MEDIUM | Attackers were counted from every lane of the side, so a neighbouring lane's wave passing within 45 m of this lane's target could start a siege this lane could not win. | Wrong population for a count | The rule was written as "the side's units near the tower" without asking which units the order moves. | Fixed: only lanes whose next target is that tower count, so the keep counts every converging lane. |
| S3 | MEDIUM | After S1's fix, a lead past its tower could still enter at the 60 m stay line itself and flip the order on each crossing. | Entry and exit bands that disagree | The first fix closed the band for one path into the state, not for the arc path. | Fixed: a siege starts only within the fire range, which leaves a 15 m band on every path. |
| S4 | LOW | The entry rule was stated four ways across the code, the feature doc and the build spec; the doc claimed unit behaviour (archers stop, 18 m) that the decompile does not support; the stay distance was a constant unrelated to `towers.fireRange`; two lines of dead code; no parity test. | Documentation and tests | The doc was written from the brief before the engine lens read the AI scorer. | Fixed text from the decompile (the engine favours a unit's current behaviour 1.2 times), stay = fire range + 15 m, dead lines removed, a parity row. |

**Tower hit points:** the challenger showed that 1200 hit points must not ship: a human-led group of 15 units in contact,
at the measured 4.7 to 5.9 hit points per second per unit, would fell such a tower in about 16 s. Hit points went back to
8000, 11000 and 18000, which the maintainer confirmed on 2026-11-01 as the target for a human-led assault (a group of
15 units should need about 2 minutes on an outer tower). Regeneration stays at 1 per second after 45 s: at 15 per second after
20 s every lull healed all the damage an assault had done (Healed equalled Applied on every tower not under attack at the
end of runs 075050, 081305, 095058 and 112404).

**Validation:** run `E:/Temp/fords-runs/20261101-143718` (2700 s, AI on both sides, 8000, 11000 and 18000 hit points, regeneration 1 per second after 45 s) started sieges on four of six streams (29 samples at 5 s, the lead a median of 3 to 28 m from the tower). No stream flipped: the most AttackTower commands any stream received in 45 minutes was 18. Contact rose from 1.1 % of flagged unit-seconds in run 123312 to 5.1 %, 8.3 % and 22.0 % on the sieging streams, West received AttackTower commands for the first time, and the most-hit tower took 4,210 hit points (the earlier best was 1,276) and fell to 5,014 of 8,000. No tower fell, and the verdicts were 7 PASS and 0 FAIL with no exception.

**Concentration:** with the AI's `lanes.assaultThreshold` raised from 40 to 70, run `E:/Temp/fords-runs/20261101-152949` (otherwise the same as run 143718) gave East 50 siege samples (17 before) and 1,204 contact unit-seconds on lane 0 (7 before); `tower_west_2_0` took 7,076 hit points and fell to 1,750 of 8,000, but no tower fell. West never sieged although its lanes held 59 to 74 live units and stayed on Push in 94 to 99 % of samples, so the higher threshold did not stall the AI; its units near the East towers never reached 1.5 times the defenders there. The threshold is 70 from this change on.

**Lane 2 contact:** an investigation of why units ordered at lane 2 towers made almost no contact (0 to 4 contact
unit-seconds against 1,826 flagged unit-seconds over five runs) found the same root cause, plus AttackTower orders that
the lane-arc reach test gave while the lead stood 26 to 236 m away inside another lane's area. Contact is measured from
the tower entity origin, and the tower geometry does not explain the gap.

FOLLOW-UP: the existing in-reach AttackTower branch still fires by lane arc at any distance, and lane leads drift into a neighbouring lane's area in about two thirds of mid-field samples (no issue: the maintainer chose no GitHub issues for Fords of War).
Follow-up closed 2026-11-04 (Mike: reopen if a big fight goes over budget): `UnitsNear` runs about 12 times per mission frame and the own-side calls go through LINQ `NextTargetOf`; measure before optimising (no issue: the maintainer chose no GitHub issues for Fords of War).
Follow-up closed 2026-11-04 (9ea4ffd44): check 9's ladder does not split tower blows by attacker class, so a ranged lane's damage cannot be told from melee contact (no issue: the maintainer chose no GitHub issues for Fords of War).
Follow-up closed 2026-11-04 (Mike, set to 1.0 in 95a348a60): the hard AI profile's `incomeMultiplier` 1.2 gives the AI more gold than a human commander, which the maintainer's design (same shop, no extra powers) does not allow for; the maintainer decides (no issue: the maintainer chose no GitHub issues for Fords of War).
Follow-up closed 2026-11-04 (Mike: an even AI match need not end in a keep; judge length with a human side): with the siege rule and `lanes.assaultThreshold` 70, a 45-minute AI match brought one outer tower to 1,750 of 8,000 hit points but felled none, and West (Gondor) never sieged; why one faction cannot win the ground at a tower, and whether an AI-against-AI match should reach a keep at all, stay open (no issue: the maintainer chose no GitHub issues for Fords of War).
