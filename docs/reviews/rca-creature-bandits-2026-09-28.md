# RCA: Creature Bandits (#692), deep-review 2026-09-28

Eight-lens `/deep-review` of the uncommitted `feat/creature-bandits` worktree (base `743818cf`) before the merge to
`bannerlord-1.5.x` and release v2.0.31. Two HIGH, six MEDIUM and about twenty LOW findings, every one re-read against
the worktree and the installed v1.5.3 decompile before it was entered here. All fixed in-session except two LOWs
that Mike accepted as known limitations and one left as a logged, documented edge; three behaviour choices went to
Mike (the brood switch, the blunt rule, the scripted blows). Full suite after the fixes: 10,968 passed, 2 skipped,
0 failed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | The 14 creature options sat in MCM group "Combat Mechanics/Creature Bandits", under a master toggle whose hint says "everything below is inert", and no creature read folds it: with Combat Mechanics off, spiders kept 50% missile damage, the tuned HP and the strike caps. | MCM master-toggle fold | The group was chosen for where the options read naturally (resistances feel like combat mechanics), and `CreatureBanditTuning.Current` reads `TaomSettings` directly, not through `CombatMechanicsSettingsProvider`, which is where every existing fold lives. Third instance of the class (CombatMechanics 2026-07-02, WotR Momentum 2026-07-03). | Moved to a top-level "Creature Bandits" group; `McmGroup_IsTopLevel_OutsideTheCombatMechanicsMasterToggle` pins it. Lesson below: a group's path is a promise. |
| 2 | HIGH | During the campaign's Order of Battle screen the creature tree kept hunting: `SetScriptedPosition` teleports while `Mission.IsTeleportingAgents` is set (`Agent.cs:2462-2465`), so each spider would land on the paused player army and strike it. | Engine lifecycle (deployment) | The spike used console spawns after deployment (`frame=initial-position` on all 9), so the battle spawn loop and its deployment phase never ran. Vanilla pauses formation members and hides humans only (`DeploymentMissionController.cs:98-119, 187-199`), and the BT framework has no deployment awareness. | `CreatureMayFightDecorator` gates the fight on `AllowAiTicking` and `IsTeleportingAgents` (the engine AI components' own check); `CreatureHoldTask` holds meanwhile. Tests: `MayFight_OnlyOnceDeploymentIsOver`, `CreatureBanditTree_HoldsUntilDeploymentIsOver`. In-game check owed. |
| 3 | MED | `SpiderAttackService` (a service) unwrapped `AgentAdapter` to the sealed `Agent` and read `Mission.Current` to feed the creature diagnostics, and the base Spider feature imported the CreatureBandits diagnostics (ADR-007). | Architecture | Playtest instrumentation was bolted onto the nearest code that knew the arc outcome, during the spike, outside the review. | The service returns a `SpiderStrikeOutcome`; the attack task (boundary code holding the `Agent`) logs it. Diagnostic lines gate on `!strike.IsCapped`. |
| 4 | MED | `CreatureBroodSpawnBehavior` was 218 lines, about 90 of them `[diag]` output, with the stray-patrol rule inline and untested (ADR-002). | Architecture | Same root as 3: diagnostics grew inside the entry point. | Diag lines moved to `CreatureBroodCampaignDiag` and a diag-only campaign behavior; the stray rule is `CreatureBanditRules.NeedsPatrolOrder`, tested. |
| 5 | MED | The prisoner rule and the spawner's error logging lived in the Diagnostics folder marked "strip after sign-off"; the strip recipe did not name those call sites, so following it would have deleted the rule or left dangling references. | Architecture / strippability | Same root: the diag facade was the most convenient home for a static logger and a counted decision. | `CreatureBanditAgents.RefusesPrisoner` (Hooks, behaviour-tested) and `CreatureBanditLog`; the feature doc's "Stripping the diagnostics" lists every call site. |
| 6 | MED | `Mission.CanAgentRout`, patched by Patch93's rout postfix, took PatchShield's per-call finalizer (a reflection-cache lock) on every AI agent's parallel tick, about 700 calls a second at 400 agents. | Harmony / PatchShield | The same change excluded the three weapon guards, the obviously hot targets, and nobody costed the rout postfix, which runs from `CommonAIComponent.OnTickParallel` for horses too. Third instance (Patch38 2026-07-10, Patch92 2026-09-26). | Excluded; `HotCreatureTargets_AreOnPatchShieldsExclusionList` walks it. Lesson extended below. |
| 7 | MED | `WeaponStateHook_TargetsTheOnlyPrivateCreateAgent_WithOneFlagsRead` reads vanilla IL but was tagged `BindingVerification` only; the hosted gate runs on reference assemblies whose `CreateAgent` body is `ldnull; throw` (2 bytes, checked), so it would fail CI after the merge. Two untagged tests enumerated TAOM's types with `Assembly.GetTypes`. | Test categories | Local runs always have `BANNERLORD_GAME_DIR`, so the failure is invisible before CI. `tests.md` "Test categories" states the rule. | Tagged `RequiresGameIL`; enumeration via `AccessTools.GetTypesFromAssembly`, the call Harmony's `PatchCategory` uses. |
| 8 | MED | The MCM off switch #692 and the roadmap promised did not exist, and the campaign path is unsmoked. | Completeness | The plan item was not carried into the build checklist. | "Spawn Spider Broods", default on (Mike, 2026-09-28); gates new broods only. |
| 9 | LOW | The routed-count backstop fired for every routed creature and logged a misleading WARNING: route A clears `Mountable`, so SandBox already counts it (`BattleAgentLogic.cs:147` skips only a mount). | Stale assumption | Route A changed `IsMount` for the creature after the backstop was written; nothing swept `IsMount`'s consumers. | The rule takes `isMount`; stale comments fixed. Lesson below. |
| 10 | LOW | A `SpawnMonster` that throws after the native creation call falls back to the husk while the native agent exists. | Engine edge | The spawner's own comment said this must not happen, but the catch sits around the whole call. | Not behaviour-fixed (no safe alternative keeps the troop counted); the fallback WARNING now records `agent had already been created` from `CreatureWeaponStateScope.ReachedCreation`, and the feature doc lists it. Never seen in play. |
| 11 | LOW | A banner bearer's spawn (`bannerItem` set) would have been swapped for a creature, and `BannerBearerLogic` uses the returned agent's weapons. | Data flow | The prefix's premise, "the loop ignores the agent", holds for one caller only. Unreachable today (a brood has no hero captain). | The prefix declines when `bannerItem` is set. |
| 12 | LOW | Stale or wrong text: co-op classification said the fingerprint is replicated (it is host-local for a `SpawnMonster` agent); the guard comment and registry said the weapon state is "never initialised"; the registry said "restores health" and "BUILT, NOT SMOKED"; the feature doc still described the shared tree and `Mountable`; the XML comment named the old tree branch; `PatchShieldPolicy` named a test that does not exist; `gamemodels.md` rule 7 ignored feature-declared models; the API snapshot was not regenerated; the reflection-site catalogue lacked the new field. | Docs drift | Route A and the tree split landed after most of this text was written, in the same uncommitted change. | All corrected; snapshot regenerated; catalogue row and `[DataRow]` added. |
| 13 | LOW | The harness exemption for the brood troops gave the wrong reason ("only the husk would wear it"); a harness in that slot is equipped on the wild spider. | Tooling text | The reason was written by analogy with the Spider Rider rows. | Reason, XML comment and the validation doc table corrected. |
| 14 | LOW | Dead 3-argument service overloads, a file named for none of its types, `EquipmentIndex.ArmorItemEndSlot` read as the Horse slot, a catch labelled with the wrong method. | Standards | Leftovers of the strike refactor. | Fixed. |
| 15 | LOW | The diagnostics gate read `ConcurrentDictionary.Count` (every lock) each frame in every mission; the captures ran the localizer inside unguarded engine callbacks, some off the main thread. | Diagnostics cost / thread safety | Same root as 3 to 5. | `Count` is the serial; names travel as `TextObject` and resolve on the main thread; each capture is guarded. |
| 16 | LOW | TAOM's scripted blows skip the damage-taken rules. | Known limitation | Accepted by Mike (2026-09-28): no effect at the 100% melee defaults. | Documented in the feature doc and on `CreatureBanditDamage`. |
| 17 | LOW | Documentation duty: no feature-map row, INDEX line or trap-index line; the feature doc lacked Dependencies, the MCM table, Performance and "new campaign only"; `spider.md` and two validation docs were not updated; four seams had no test (the Custom Battle model's call, rider and horse in one slot, the clan XML contract, the campaign console origin). | Completeness | The doc was written before route A and the review. | All added; four tests added. |
| 18 | LOW | Convergence pass: the new brood switch was inserted above the property carrying `GroupOrder = 53`, and MCM takes a group's order from the first property it meets, so the group sorted at 0. | Step 4 edit | The edit script inserted the switch before the anchor line it searched for; the MCM group test checked the path, not the order. | GroupOrder moved to the first property; the MCM group test now asserts it there (RED at 0 before the fix). |
| 19 | LOW | Convergence pass: no test failed if the switch's read were dropped from the daily tick. | Test coverage | Only the pure rule was tested. | `BroodSpawner_ReadsTheMcmSwitchEachDay` pins the read and its use. |

The convergence pass also noted that vanilla's blunt rule applies the Blunt factor to a charge or kick whatever raw type the native side wrote, including a type outside Cut, Pierce and Blunt, which is what vanilla's own damage math does; no change.

Design proposals applied (not defects): the morale rule moved from a patch to the morale models' `CanPanicDueToMorale`; the anchor pick uses `MBRandom.RandomInt`; the tuning keeps one strike set; the rider-in-arc check is derived once; the troop id set is built once; the blunt rule is vanilla's (Mike approved). Not applied: collapsing `CreatureWeaponStateScope`'s `Done` phase into `Idle`, because `Done` now carries "the native agent exists" for finding 10.

## Codex adversarial review (gpt-6-astra, ultra), first pass

Two MEDIUM defects, both confirmed against the installed engine and fixed; all six Known Suspects DISPUTED with
evidence; two LOW observations.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| C1 | MED | Creature bandits never reached the battle scoreboard: `BattleObserverMissionLogic` reports only `IsHuman` agents for spawns, casualties and kill credit (v1.5.3 `:35-76`), so every brood battle showed the spiders' side empty and no kills either way, in the campaign and Custom Battle. | Missing vanilla gate | The engine lens read this exact gate and filed it harmless ("the counts stay symmetric"): it checked accounting, not what the player sees. Vanilla skips mounts because the rider is the troop; route A made the creature the troop. | `CreatureScoreboardBridge` makes vanilla's three calls for a creature; `ScoreboardCasualty` and `ScoreboardBridgeCreditsKill` are table-tested; the wiring test pins the calls. Lesson below. |
| C2 | MED | A capped strike held every strikeable agent the arc reported, and a teamless loose horse is on nobody's side, so a nearer loose horse took a one-target bite from the soldier who opened the engage gate. | Logic error | The cap was reviewed against soldiers, allies and a rider's horse; the engage gate skips mounts, the damage filter did not, and nobody set the two side by side. | A capped strike skips riderless mounts; `CappedStrike_ALooseHorseNearer_NeverTakesTheSoldiersSlot` (RED before the fix). The ridden spider's uncapped path is unchanged. |

Second pass, over those fixes (gpt-6-astra, ultra; `high` would have fitted a two-fix pass, and the review skill now
lets the session size the effort):

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| C3 | MED | The C1 bridge demanded a non-null `Origin.BattleCombatant`, but a Custom Battle console spawn's `BasicBattleAgentOrigin` has none (v1.5.3 `BasicBattleAgentOrigin.cs:25`), so console-spawned creatures still never reached the scoreboard. Vanilla's observer asks only for a team, an origin and a troop, and the scoreboard files a null combatant under a generic party (`SPScoreboardPartyVM`). | Guard stricter than its consumer | The guard was written defensively from the signature, not from what vanilla's observer and the scoreboard accept; the tests pinned the source text, not the origin types that reach the bridge. | `CreatureBanditRules.IsScoreboardRow` (team, origin, troop), tested with a real `BasicBattleAgentOrigin` (RED before the rule existed). The pass also noted that nothing tied a removal to an earlier add: the bridge now keeps a reference-keyed set of added creatures, so a creature leaves the scoreboard only once and only if it was added, and a creature is credited only while it has a row. |

The second pass DISPUTED double counting against vanilla's own observer calls and confirmed the thread model, the
loose-mount rule for route A creatures and both convergence fixes. Its one observation: a riderless elephant still
carrying howdah crew now counts as a loose mount for a capped strike. The crew stay separate human targets, so the
empty body taking no slot is the intended rule; recorded in the feature doc.

A Claude convergence reviewer then checked the C3 fix: no behavioural defect. It found a stale thread comment on the
mission behavior (it still said removal touches no TAOM collection; it now names the bridge's concurrent set) and
one LOW edge: a creature with no row could still credit its killer, where vanilla credits a kill only inside the
victim's own row gate (`BattleObserverMissionLogic.cs:54-75`). The bridge now requires the victim's row first.

Observations, not fixed: O1, the creation scope is not reentrancy-safe (a same-thread agent creation inside a
creation listener would take the outer strip; no such listener exists), recorded in the feature doc; O2, a brood
can gain rescued looters as vanilla lets any bandit winner, recorded as a known limitation.

## Root-cause patterns

**Temporary diagnostics built as production code (3, 4, 5, 15).** The playtest logging was the right call (lesson "Add comprehensive diagnostic logging to live-only behavior, then strip after sign-off"), but it was added wherever the data was nearest: a service, an entry point, the reward model's decision, the error log. Temporary code that owns a rule, a logger or a sealed-type unwrap is no longer temporary; stripping it breaks production.

**A change to the creature's engine identity not swept through its consumers (9, 12).** Route A clears `Mountable`. Everything written earlier that reasoned about "a riderless mount" (the backstop, the guard comments, the registry, the feature doc) was stale the moment it landed. graphify cannot see an engine flag, so the sweep has to be a grep for the flag's readers.

**A spike's coverage taken for the feature's (2).** The route A spike proved targeting through console spawns. The battle spawn loop, its deployment phase and the campaign path are a different code path the spike never touched.

## Why each lens caught or missed

- **Standards (1)** caught 3, 4, 5 and most of 14; it does not trace MCM reads, so it could not see 1.
- **Engine (2)** caught 2 (decompiled the deployment controller and `SetScriptedPosition`), 9, 10, 11's engine side and the wrong text in 12.
- **Efficiency (3)** caught 6 and 15.
- **Completeness (4)** caught 7, 8 and 17; it reproduced the CI stub from the reference assembly.
- **Data flow (5)** caught 1 (its master-toggle fold rule, 2b) and independently found 2, 9, 11 and 16. Again the highest-value lens.
- **Design (6)** found no defects; its proposals are listed above.
- **XML (7)** found one stale comment and listed the in-game checks owed.
- **Tooling** caught 13 and the two validation doc tables.

## Lessons appended

- `lessons/adapters-taleworlds-api.md`: an agent that stands in for a troop must reach every `IsHuman`-gated
  consumer the troop would (Codex C1).

- `lessons/gamemodels-services.md`: a sub-group under a master toggle inherits its promise.
- `lessons/adapters-taleworlds-api.md`: gate a non-formation agent's scripted moves on the deployment signals; sweep a cleared engine flag's consumers.
- `lessons/harmony-il.md`: the PatchShield exclusion covers every per-agent target of a category, not only the obvious one.
- `lessons/build-tooling-workflow.md`: keep temporary diagnostics strippable.
- `lessons/testing-qa.md`: a console-spawn spike does not cover the battle spawn path.
