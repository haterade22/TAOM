# RCA: SignatureStrikes deep review (#605), 2026-09-16

## Top-line

The five-agent `/deep-review` of the new SignatureStrikes feature (direction-mapped melee effects
for Sauron) returned one HIGH, one standards breach, two MEDIUMs and three LOWs. Nothing was a
wrong number or a wrong engine call: compatibility verified 38 engine members against the installed
v1.5.3 DLLs with zero incompatibilities, and the data-flow pass traced 32 flows with zero
inconsistencies between the two parallel paths (the model's primary-victim verdicts and the mission
logic's ring). Every finding was a limb dropped while mirroring a sibling, a count not taken, or a
test not written for a class the author had filed as "engine only". All were fixed the same session;
suites green after the fixes (207 in the feature filter, full suite below).

Two more defects were caught by the author's own scan, not the agents: two long dashes in produced
prose, one of them put there by the Write tool itself.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (reviewer) / MED (confirmed) | `SignatureStrikesMissionLogic` registered agents through `OnAgentBuild` only. `DreadAuraMissionLogic` and `WargMissionBehavior`, the two trackers it was modelled on, both carry a one-shot scan of the agents already on the field for the case where an agent was built before the behavior could see it. Without it Sauron would be silently absent from exactly the battles the feature targets, with no log line to say so. | Lifecycle, event coverage | The author judged `OnAgentBuild` sufficient (behaviors initialize before spawns in a field battle) and dropped the scan as redundant. The engine path in which `OnAgentBuild` is missed was NOT proven by the reviewer or the author; what is confirmed is that both siblings scan, and the cost is one loop per mission. Filed as confirmed precedent deviation, unproven engine gap. | Scan added on the first eligible tick (idempotent; `TryRegister` now returns true only when it added, so a re-scan logs nothing twice). Lesson below: mirror every limb of a sibling tracker or write down why each dropped limb is safe. |
| 2 | Standards | `SignatureStrikesMissionLogic.cs` was 158 lines against ADR-002's 150. | Thin entry point | The file was written to the DreadAura shape (per-callback try/catch, stand-down, doc comment) and never counted before review. | Trimmed to 148: registration log moved into the roster, `OnAgentRemoved` dropped (the roster evicts on `OnAgentDeleted`, the callback the index is recycled from), the scan folded into the tick's existing try block, empty base calls and in-method blank lines removed. Habit: `wc -l` on every entry point before the review gate. |
| 3 | MED | `SignatureStrikesSettingsProvider.IsEnabled` read `TaomSettings.Instance` twice per call, on every melee hit a signature hero lands. | Efficiency | Copied the two-clause fold from the sibling without hoisting the instance read. | One local read. |
| 4 | MED | No tests for `SignatureAgentRoster`, `SignatureMissionGate`, `StrikeContextFactory` (the Hooks classes), against the 80% Hooks bar. | Test coverage | The author filed the whole Hooks folder as "needs a live `Agent`" and skipped it. Three of the four have pure halves: the two enum mirrors, the item-type gate, the roster's null and lifecycle paths. | `StrikeContextFactoryTests` (8), `SignatureAgentRosterTests` (5), `SignatureMissionGateTests` (1) added. The agent-reading halves stay on the binding tests and the smoke. |
| 5 | LOW | JSON `enabled` is consulted only when MCM is absent; once MCM loads, the toggle owns it. Same as every sibling, but the JSON did not say so. | Config documentation | Pattern inherited silently. | `_comment_enabled` added to the shipped file. |
| 6 | LOW | Shipped ids `lord_1_17` / `sauron` were pinned only as config strings, not against `lords.xslt`, so a lord regen dropping the race attribute would pass every test. | Cross-file pin | The DreadAura shipped test has no such pin either; UncapturableHeroes does. | `ShippedConfig_SauronStillCarriesTheSauronRaceInLordsXslt` reads the `lord_1_17` template block and asserts `race">sauron<`. |
| 7 | LOW | `StrikeCollision.None` has a producer arm (`MapCollision` default). Codex (review 114) corrected the first wording here: `CombatCollisionResult.None` IS an engine member, so `None` maps to `None` by the explicit switch case and the default arm is what a future member would fall into. | Defensive default | Not a defect. | Kept: the safe shape. |
| 8 | Self-caught | Two long dashes in produced prose: a rewritten model comment kept its old em dash, and `ShippedSignatureStrikesConfigTests` got literal en/em characters where the source was written as `\u2013\u2014`. | Output style | The Write and Edit tools decode `\uXXXX` in their parameters, so a C# escape typed into them lands as the character. The Bash heredoc eats one backslash round in the other direction. | Both fixed. Lesson below: build escape sequences from char codes in a script when a source file must contain a literal backslash-u. |

## Codex pass (review 114, gpt-6-astra ultra, same day)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | P2 | `StandDown` set `_disabledForThisMission` and cleared the queue but left the roster populated; `TaomCombatMechanicsModel` probes that roster for the primary-victim verdicts, so after one caught exception every overhead past the old stamp was granted with no ring and no renewed cooldown. | Stale state, shared consumer | The stand-down was written from the logic's point of view (its own callbacks stop) and never asked what else reads the state it stopped maintaining. The five deep-review agents traced the roster's lifecycle through the logic's callbacks only. Same class as the tournament-exit "outermost gate" lesson: a disable in one layer with a consumer in another. | `StandDown` clears the roster (registration and the scan are already gated on the flag, so nothing re-adds). Lesson in `lessons/state-lifecycle-save.md`. |
| C2 | P3 | `StrikeNames` used `Enum.TryParse` + `Enum.IsDefined`; `"1"` parsed to Overhead and passed `IsDefined`, so a numeric typo became a live row. The comment claimed the opposite. | Validation | Assumed `IsDefined` rejects what `TryParse` accepts from a digit string; it does not (it checks the VALUE). No parser test existed. | Name-only matching over `Enum.GetNames`; `StrikeNamesTests` (10) plus two provider tests for numeric keys and kinds. |
| C3 | P3 | A NaN `CollisionGlobalPosition` reached `GetNearbyEnemyAgents` before any finiteness gate. | Engine-float gate, category 2 | The falloff gated distance AFTER the native query; the boundary itself was the unguarded input. | Finite check on the impact before the query. |
| C4 | P3 | The shield-basis cast `(int)Math.Round(damage * multiplier)` lacked the ring cast's overflow guard two methods away. | Float-to-int cast, category 3 | The guarded cast was written first and the second cast copied the arithmetic without it. | One `RoundToDamage` for both; `Evaluate_ShieldBlockBasisPastIntRange_ReturnsNull`. |
| C5 | P3 | A configured cooldown of 0 (accepted by the [0, 120] range) defeats one-package-per-swing: a cleave's second body rings again. | Validation, ordering invariant | The range was chosen for "no negative", not for the invariant the doc promises. | 0.5 s floor; `GetConfig_ZeroSlamCooldown_Reverts...`. |
| C6 | P3 | A profile with no damage share and no fear still queried the proximity map and wrote an INFO line per swing. | No-op path | The ring's per-victim `damage <= 0` skip hid that the whole effect was inert. | The runner returns before the query when the effect can apply nothing. |

Codex disputed six of the ten handed suspects with decompiled evidence and left three honestly
UNVERIFIED: whether native honours `BlowFlags.KnockBack` on a synthetic blow, the thread of the
native `MeleeHitCallback` (the `[MBCallback(null, false)]` attribute declares it non-multithread
callable, which supports the main-thread design without proving it), and whether a terrain hit
carries a non-null `realHitEntity` (if it does not, `flag5` cancels the callback and the ground slam
never fires). All three are smoke items, recorded in the feature doc.

## In-game smoke, first attempt: inert, no log line (#606)

Mike played Sauron alone against 36 looters (`taom_debug_2026-09-16_16-45-48.log`, 16:59:30 to
17:01:09) and saw nothing. The log had the config load and the behavior added, then silence: no
gate line, no registration, no stand-down. DreadAura registered him on the same identity axes.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| S1 | HIGH (shipped) | The mission gate and the state reset lived in `OnBehaviorInitialize`, which the engine dispatches to the behaviors already in the list (`Mission.AfterStart`, `Mission.cs:3827`) BEFORE `SubModule.OnMissionBehaviorInitialize` adds TAOM's (`:3831`); `AddMissionBehavior` calls only `OnCreated` (`:4699`). `_eligible` stayed false and every callback returned before its first log line. | Engine lifecycle virtual, firing set | The virtual's NAME was trusted. Five deep-review agents and a Codex ultra pass verified its signature and never asked whether it fires for a behavior added the way TAOM adds them. The DreadAura precedent reads its gate every tick and never overrides this virtual; the copy cached the gate at init as an improvement. **Repeat offender:** the same lesson was written four days earlier for `MBSubModuleBase.OnGameLoaded` (`lessons/state-lifecycle-save.md`). The grep for other overriders found SmartCavalryAI, MixedFormations and SiegeDismount in the same dead callback; their init log lines exist in no log on disk. | Gate read on first use, reset in `OnCreated`; stage logging at every step; `MissionBehaviorLifecycleTests` fails any new TAOM override of the dead virtual; `harmony-patches.md` states the firing set; the deep-review compatibility prompt asks for the caller of every overridden lifecycle virtual. The three siblings are #606's remaining work. |

## Root-cause pattern

Findings 1, 3, 5 and C1 share one shape: **a sibling was mirrored for its structure and not audited limb
by limb.** The DreadAura logic was the template for the mission logic, its settings provider for the
provider, its config for the JSON comments. Each time a limb was dropped or simplified without a
written reason (the scan, the hoisted instance read, the MCM note), and each dropped limb came back
as a finding. This is the same category as the gamemodels lesson "when you keep N-1 limbs of a
faithful port, re-audit limb N": the limb you leave out is the one that carried the reason.

## Why each agent missed (or caught) these

- **Agent 1 (Standards)** caught the line count (#2); nothing else in its rule set applies to the others.
- **Agent 2 (Compatibility)** verified 38 members and three call-order claims; none of the findings is an API question.
- **Agent 3 (Efficiency)** caught #3 and correctly reported the non-capturing lambda as UNVERIFIED rather than HIGH; Roslyn caches non-capturing lambdas in a static field, and `CustomAttacksUtils.TakeDamage` already uses the same shape per hit.
- **Agent 4 (Completeness)** passed the feature as COMPLETE, because its test-existence rule asks whether a test FILE exists per new class and the Hooks classes had none; it did not ask which halves of a boundary class are pure. The data-flow agent asked and found #4.
- **Agent 5 (Data flow)** caught #1, #4, #5, #6, #7. Its lifecycle matrix and event-coverage checks are the ones that fire on a dropped tracker limb.
- **Nobody** caught #8: no agent reads produced prose for dash characters; `tools/lint_docs.py` covers markdown only, and the two hits were in `.cs` files.

## Feedback memories to codify

One durable lesson from #1 (below, in `docs/reviews/lessons/state-lifecycle-save.md`) and one
tooling lesson from #8 (`docs/reviews/lessons/build-tooling-workflow.md`). The line-count habit
(#2) is a one-liner on an existing ADR, not a new rule.
