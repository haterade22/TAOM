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

FOLLOW-UP: AI steering to capture points (no issue: creating an issue needs the maintainer's word).

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
