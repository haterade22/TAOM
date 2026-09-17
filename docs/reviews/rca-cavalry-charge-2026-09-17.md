# RCA: cavalry charge knockdown v2 and per-culture charge damage deep review (#610), 2026-09-17

## Top-line

The five-agent `/deep-review` of #610 (vanilla-parity charge knockdown with live sliders, plus a
per-culture multiplier on `MountChargeDamage`) returned one HIGH that three of the five agents
found independently, one MEDIUM, and four LOWs. The HIGH made the headline half of the change a
no-op: the multiplier was looked up with `agent.Character` on the MOUNT agent, and a battle mount
is built with a null `Character`, so every horse in every battle multiplied by 1.0. Nothing in the
knockdown half was wrong: the NaN gates, the two-surface clamp invariant, the parallel-branch
consistency and the JSON-to-consumer wiring all traced clean, and compatibility verified every
engine member against the installed v1.5.3 DLLs. All findings were fixed the same session; the
CombatMechanics filter is 184 green, full suite below.

This is a repeat of a lesson already on file (`rca-career-phantom-passives-2026-06-26.md`,
`lessons/adapters-taleworlds-api.md` "a battle mount spawns with no Origin"): a mount agent carries
none of the identity a human agent carries, and every identity read on a mount must hop through
`RiderAgent`. The earlier lesson was written for `Origin`; this one was `Character`. The class is
the same and the lesson is now written for the class.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `TaomAgentStatCalculateModel.UpdateAgentStats` multiplied `MountChargeDamage` by `Multiplier(agent.Character?.Culture?.StringId)` on the mount. `Mission.CreateHorseAgentFromRosterElements` builds the horse with `CreateAgent(..., characterObject: null)` (v1.5.3 `Mission.cs:4611`), so the key was always null and `Multiplier(null)` is 1.0 by design. No crash, no log; the feature shipped inert. The code comment and the feature doc both asserted the opposite ("a mount agent's Character is its rider's"). | Engine identity on a sub-kind of agent | The author stated an engine fact from memory and never opened the constructor path. The evidence against it was in the same file: `GetEffectiveMaxHealth` two methods above hops `agent.IsMount ? agent.RiderAgent : null`, and the same-day sibling `AgentAggressionApplier.CultureOf` returns null for a mount on purpose. The "career passive already finds the rider this way" justification cited `ApplyAgentStatModifiers`, which returns before touching a mount (`if (!isHuman) return;`), so it never found anything on a mount. The unit tests could not see it: `Agent` is sealed and `ChargeDamageServiceTests` passes strings straight to the service; the "mount with no culture returns 1.0" case was listed as one edge case among several when it was the universal case. **Repeat offender:** `rca-career-phantom-passives-2026-06-26.md` finding 1 was the same mount-has-no-identity class on `Origin`. | `MountChargeDamageApplier.RiderCultureOf` hops through `RiderAgent`, with the engine evidence in its doc comment; `MountChargeDamageBindingTests` pins the hop at the IL level (`get_RiderAgent` must appear in the lookup, both stat models must call the applier). Lesson widened from `Origin` to every identity field on a mount. Doc and comment corrected. |
| 2 | MED | Custom Battle registers its own `AgentStatCalculateModel` (`TaomCustomBattleAgentStatCalculateModel`, `SubModule.RegisterCustomBattleModels`) and it was never given the charge service, so the cheapest smoke route (Custom Battle, Rohirrim into a Mordor line) would not have shown the multiplier even after finding 1 was fixed. | Second registration of the same slot | The campaign slot was the one the feature was written against; the Custom Battle slot was added by another feature (#608) the same day and the #610 author never enumerated registrations of the overridden model. | The Custom Battle model takes the same optional service and calls the same applier; `SubModule.cs` passes it; the binding test covers both models. |
| 3 | LOW | The `ChargeMinPenetrationFactor` MCM slider's upper bound (2.5) is a compile-time mirror of the shipped JSON `maxPenetrationFactor`; the provider clamps to the live JSON value, so a JSON retune below 2.5 would leave the slider showing a range the clamp silently cuts. | Two-surface invariant, hint text | The auto-ratio slider got its "floor follows neutral" sentence; this one did not. | Hint text now states the ceiling is the JSON maximum and that higher values are treated as it. |
| 4 | LOW | `docs/features/combat-mechanics.md` repeated the false identity claim and its MCM summary line listed 4 of the 17 group members. | Doc drift | The summary line predates Signature Strikes and #610; neither feature updated it. | Both paragraphs rewritten with the engine evidence and the full member list. |
| 5 | LOW | Missing edge-case tests: infinite culture multipliers of either sign, the inclusive 0.1 and 5.0 bounds, and the Branch B weight term clamping to `maxPenetrationFactor`. | Test coverage | The NaN and out-of-range cases were written; the boundaries and the infinities were assumed covered by `FiniteFloatValidator`. | Three tests added (`...Infinite_DropsBothSignsAndWarns`, `...AtTheBounds_KeepsBothEnds`, `...WeightTermAboveMax_ClampsToTheJsonMaxPenetrationFactor`). |
| 6 | HIGH (re-check) | The fix for finding 2 multiplied `MountChargeDamage` from the Custom Battle model's `UpdateAgentStats`. `CustomBattleAgentStatCalculateModel` writes the property once, in `InitializeAgentStats` (v1.5.3 `:48`), and its `UpdateHorseStats` (`:362-396`) never rewrites it, so every re-run of `UpdateAgentStats` (native `Agent_UpdateAgentStats`, the 13 `UpdateAgentProperties` call sites) multiplied the already-multiplied value: `raw x m`, `raw x m^2`, ... The Sandbox model rewrites the property every call (`:1280`), which is why the campaign placement is idempotent and the copy of it was not. | Two base models, one property, different lifecycle methods | The Custom Battle model was written by mirroring the campaign model's placement without reading the Custom Battle base's body for the property. Found by the one-agent re-check the skill asks for after fixes. | The Custom Battle model applies the factor from its own `InitializeAgentStats` override, right after the base write, once per spawn; the applier's doc comment names the two placements; `MountChargeDamageBindingTests` asserts the Custom Battle `InitializeAgentStats` calls the applier and its `UpdateAgentStats` does not. Consequence recorded in the doc: in Custom Battle the factor follows the spawn rider. |
| 7 | LOW (re-check) | `RiderCultureOf_HopsThroughRiderAgent` collected call NAMES into a set; `mount.Character` and `mount.RiderAgent.Character` compile to the same `get_Character` token, so a regression that read the mount's Character and touched `RiderAgent` elsewhere would pass. | Test strength | A subset assertion on names was written for readability. | The test now keeps the IL call sequence, requires exactly one `get_Character`, and requires `get_RiderAgent` before it. |
| 8 | Note, not fixed here | `CareerAgentStatService` multiplies `MountChargeDamage` on the RIDER's driven properties (`:143`, `:160`, `:178`, all under `if (!isHuman) return;`), and the engine reads the property off the MOUNT per charge hit (`AttackInformation.cs:342` from the attacker, and `Mission.ChargeDamageCallback`'s attacker is the horse, `Mission.cs:6103`). The career `MountChargeDamage` passive and the two charge-damage buffs are therefore the #394 blind spot in `PassiveEffectConsumers.cs:19`: read, but not where the player expects it. Pre-existing, outside #610's diff, needs its own issue. | Phantom passive (#394 class) | Found while establishing which agent the engine reads the property from. | Reported to the maintainer for an issue; the fix is one `agent.RiderAgent` hop in the mount branch of the same model. |

## Root-cause pattern

Findings 1 and 8 share one shape: **a mount agent is not a human agent with a horse skin.** It has
no `Origin` (2026-06-26), no `Character` (today), and the engine reads its charge property from it
rather than from its rider (today, the inverse direction). Any identity or property routed by
"the agent" without asking which of the pair (mount, rider) the engine actually consults is a
coin flip that lands wrong silently: the mount branch returns null and every guard downstream
treats null as "no effect".

Findings 2 and 6 are the registration cousins: a `GameModel` slot overridden in two places
(campaign and Custom Battle) gets a feature added to one of them, and when it is added to the
second by copying the first, the two BASE models write the same property in different lifecycle
methods, so the copied `*=` is idempotent in one and compounding in the other.

## Why each agent missed or caught these

| Agent | Finding 1 | Finding 2 |
|---|---|---|
| 1 Standards | Missed: checked ADRs and validation, not engine facts. Correct scope. | Missed: registration check was "is the service registered", not "is every registration of the overridden model given it". |
| 2 Compatibility | **Caught.** The prompt asked whose culture a mount's `Character` carries; the agent decompiled `CreateHorseAgentFromRosterElements` and found the null. | Missed: lifecycle check covered the campaign `AddModel` only. |
| 3 Efficiency | **Caught**, while establishing call frequency for `UpdateAgentStats` on mounts. | Missed. |
| 4 Completeness | Missed: counted tests, did not ask what the tests could not see. The "mount with no culture returns 1.0" edge case read as coverage. | Missed. |
| 5 Data flow | **Caught**, with the same-file contradiction (`GetEffectiveMaxHealth`) and the same-day sibling (`AgentAggressionApplier.CultureOf`). | **Caught**, from the MCM toggle enumeration: the toggle's consumer was reachable from one of two registrations. |

Three agents catching the same HIGH is the review working as designed; the reason it reached the
review at all is the author's own step: an engine fact written into a code comment and a doc
without a decompile behind it, against `evidence-over-claims.md` C.

## Lessons appended

- `lessons/adapters-taleworlds-api.md`: a battle mount has no `Origin` and no `Character`; every
  identity read on a mount, and every per-charge property the engine reads, routes through the
  mount/rider pair deliberately, with the direction taken from the engine's consumer.
- `lessons/gamemodels-services.md`: a model slot registered in two starters (campaign and Custom
  Battle) gets every new rule in both, placed per BASE model's write site, and a binding test pins
  both placements.

## Verification

- `dotnet test TAOM.Tests --filter "FullyQualifiedName~CombatMechanics|FullyQualifiedName~SettingsFingerprint|FullyQualifiedName~GameModelOverrideBinding|FullyQualifiedName~CultureDoctrine"`: 458 passed, 0 failed (after the re-check fix).
- Full suite: see the CHANGELOG entry for the run quoted at commit time (the other session's
  in-flight CultureDoctrine work carries its own failures, none in CombatMechanics).
