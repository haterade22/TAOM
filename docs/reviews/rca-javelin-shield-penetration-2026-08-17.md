# RCA: javelin shield penetration shipped a 3.33x buff (2026-08-17)

A player report ("javelins seem to be doing massive damage") traced to one mechanic: CombatMechanics
shield penetration, shipped ON since 2026-07-02 with a `runtimeShieldDamageCorrectionDivisor` of 0.3.
The divisor was a workaround for a native engine bug that, on v1.4.8, does not exist for the one
weapon class the workaround was pointed at. It now ships OFF with empty grant lists.

Everything else on the javelin path was already stock vanilla: no `Items` XML node in TAOM's module,
no `StrikeMagnitudeCalculationModel` override, no Harmony patch touching damage, and `Throwing` is
the lowest-ceilinged skill in the mod. The shipped mechanic was the whole story.

**Update 2026-09-06 (#554).** That last clause is still true of the curve: `Throwing` tops out at 100
in `GROUP_BASELINES` against `Bow`'s 320. It now has one documented exception. A troop whose only
ranged option is a thrown weapon takes the Ranged Bow curve on `Throwing` instead, so five javelin
skirmishers sit between 85 and 235. If a javelin damage report arrives for one of those troops, the
skill is no longer the part that can be ruled out on sight.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `runtimeShieldDamageCorrectionDivisor: 0.3` multiplied javelin shield damage by 3.33x to correct a native underestimation that does not apply to `Javelin` | Engine misread | The engine method was grepped for the branch that was expected (the flag check), which IS present, so a targeted read confirmed the model instead of refuting it | Read the full control flow of an engine method, not the line that matches the hypothesis. Lesson appended to `lessons/gamemodels-services.md` |
| 2 | HIGH | The inflated shield damage fed the penetration gate, so javelins passed THROUGH shields that should have blocked them | Downstream coupling | The value was reasoned about as "shield durability", a cosmetic axis. Nobody asked what else reads it | Ask of any scaled damage value: what else consumes this number downstream? Same lesson entry |
| 3 | MED | The workaround shipped ENABLED while its own comment said "config-gated pending 1.4.6 re-verify in a control battle" | Process | An owed verification recorded in the feature doc, the CHANGELOG and issue #320 is a memo, not a gate. Nothing prevented shipping the unverified default | A compensation for an unconfirmed external bug ships DISABLED; enabling is gated on the measurement. Same lesson entry |
| 4 | MED | The grant had no attacker filter, so every AI skirmisher ignored the player's shield too | Scope | The mechanic was designed as a player-facing flavour buff; the model override has no attacker parameter to filter on, and nobody noticed the asymmetry was absent | Documented in the feature doc. Not independently mechanizable |
| 5 | LOW | Nothing pinned the shipped JSON or the MCM default; only the compiled defaults were covered | Test gap | `CombatMechanicsConfig.cs` asserted in a comment that compiled values "must match the shipped JSON" with no test behind it. Restoring `"Javelin"` to the JSON would have re-armed the mechanic with the whole suite green | `ShippedCombatMechanicsConfigTests` added, modelled on `ShippedDreadAuraConfigTests`. Mutation-verified: re-adding `"Javelin"` fails 2 tests |
| 6 | LOW | Service and interface comments still asserted the disproved premise as live fact, contradicting the sibling config file | Doc drift | The fix updated the file whose values changed and the feature doc, but not the two comment headers one folder over that state the justification | Caught by the docs sweep; both rewritten |
| 7 | LOW | CHANGELOG said "Existing players must turn it off themselves" and then explained why they need not | Self-contradiction | Written before the data-flow trace established that empty grant lists make the mechanic inert regardless of a persisted MCM toggle | Corrected. The trace is now the documented reason the lists were emptied rather than only the flag flipped |

## Root-cause pattern

**A workaround for an unconfirmed bug, shipped on, with its verification deferred to a test nobody
ran.** Findings 1, 3 and 7 are one story. The original author knew the premise was unverified and
wrote that down in three places. What was missing was not knowledge, it was a gate: the default
shipped in the state that assumed the bug was real, so six weeks of players ran the compensation for
a bug that never existed on this engine. The correct polarity is the reverse. An unverified
compensation ships off, and the measurement is what turns it on.

Finding 2 is what turned a balance rounding error into a player-visible bug. `CalculateShieldDamage`
reads as a shield-durability knob, so a multiplier there looks bounded. It is not: the returned value
becomes `attackCollisionData.InflictedDamage`, which `Mission.HandleMissileCollisionReaction` tests
against `ShieldPenetrationOffset + ShieldPenetrationFactor * shieldArmor`. Scaling it silently scaled
how often missiles bypassed shields entirely.

## Why each review agent missed these

The 2026-07-02 deep review passed this feature, and its Codex pass returned CLEAN (0 P1/P2).

| Agent | Why it missed |
|---|---|
| Standards | The code is textbook: thin override, service delegation, adapter-free primitives at the boundary. Nothing about a wrong *constant* is a standards question |
| Bannerlord API compatibility | Verified that `CalculateShieldDamage` and `DecideMissileWeaponFlags` exist with matching signatures, which they do. Signature verification never asks whether the override's premise about engine behaviour is true |
| Efficiency | Correctly found the per-hit path allocation-free |
| Completeness | Tests, feature doc, CHANGELOG and issue all existed. The owed A/B was recorded as owed, which reads as tracked rather than as blocking |
| Data flow | Traced config to consumer and found it connected, which it was. The gap was not a disconnected flow; it was a connected flow carrying a wrong number |
| Codex | Returned CLEAN. It reviewed the code as written against the stated intent, and the code correctly implements the stated intent. The intent was wrong |

The common blind spot: **every agent checked whether the code does what it says. None checked whether
what it says is true of the engine.** A constant justified by a claim about external behaviour needs
that claim verified, and no agent owned that question. The 2026-08-17 review added an explicitly
adversarial agent whose only job was to refute the engine claim; it decompiled the installed DLLs
independently and reproduced the finding, which is how the claim reached CONFIRMED rather than
"believed".

## Preventive actions

1. **Lesson appended** to `docs/reviews/lessons/gamemodels-services.md`: "A workaround for an
   unconfirmed engine bug must ship OFF". It covers the polarity rule, the downstream-consumer
   question, and the empty-data-list corollary for MCM-persisted toggles. Index count corrected
   40 → 42.
2. **`ShippedCombatMechanicsConfigTests`** now pins all three surfaces (shipped JSON, compiled
   defaults, MCM default) and their agreement. Mutation-verified.
3. **Deferred, not done:** no rule yet mechanizes "an engine-behaviour claim in a comment must cite a
   decompile". That is the generalizable form of finding 1, and it would need a reviewer prompt
   change rather than a checker. Recorded here so it is not mistaken for handled.

## Owed

The in-game A/B (#320 item 4) confirming shields stop javelins again. It must use a **hero** thrower:
`CharacterObject.GetPerkValue` returns `false` for non-heroes, so vanilla's Impale grant never reached
line troops, and a troop thrower would read "blocked" whether or not the change worked.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
