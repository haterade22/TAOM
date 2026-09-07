# Lessons — Misc

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Misc lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### Confirm a cumulative-isolation suspect with a single-variable control
When isolating a bug by disabling suspects **cumulatively** (off A, test; off A+B, test; off A+B+C, test…), a result only proves the last-disabled thing was **necessary** for the no-bug state — NOT that it is the **sole** or **sufficient** cause. After a cumulative ladder localizes a suspect, run the complementary single-variable control: disable ONLY that suspect (everything else restored) AND/OR enable ONLY that suspect (everything else off); the cause is confirmed only when both the necessity and isolated tests agree. Never say "X is the cause" / "X is the sole cause" from cumulative data alone — assume ≥2 independent contributors until a single-variable test clears the others.
- **Why missed:** Elephant "slide" debug, 2026-06-10 — concluded a root cause from cumulative results twice and was refuted both times by the user's control test. (1) "It's the `as_elephant` data" — refuted: the all-disabled build had no slide, so it was the C# crew, not data. (2) "The crew is the *sole* cause" — refuted: crew-off + everything-else-on **still slid**, revealing **bone-tracking** as a SECOND independent source (its `bo_` physics floor, `SetFrame`'d at the spine bone, overlaps the elephant capsule and the solver shoves it).
- **Prevent:** Honor the user's "disable one at a time" + "keep X off, turn the rest back on" control instinct; don't bundle multiple new disables per test (the rungs 2+3 bundle and skip-to-rung-6 were both corrected). Documentation cadence: update the feature doc each isolation step, not just at the end — losing the per-rung trail forces re-derivation.
- **Source:** memory/feedback_cumulative_isolation_needs_single_variable_control.md (elephant feature; companion to feedback_root_cause_mandatory + feedback_simpler_fix_first; generalized by the always-load evidence-over-claims rule)

### Build the full system, not an MVP or phased port
When porting or building a new feature system, implement the FULL system — not a "minimum viable" or phased-deferral approach. Scope ALL components (UI, battle effects, abilities, save/load, campaign behaviors, etc.) as first-class deliverables together; don't suggest deferring subsystems unless the user explicitly asks for phased delivery.
- **Why missed:** The user explicitly corrected a plan that proposed deferring abilities, mutations, battle effects, and career buttons to "phase 2."
- **Prevent:** When scoping an implementation plan, enumerate every subsystem up front and treat each as a required deliverable; flag any deferral for explicit user sign-off rather than assuming it.
- **Source:** memory/feedback_career_full_product.md (career-system feature)

### Don't add aspirational enum values or state fields
Do NOT add enum values, list fields, or status flags "for future use" unless there is a concrete caller in the SAME PR that produces or consumes them. Every enum value needs ≥1 producer (returns it) AND ≥1 consumer (matches on it); every status/outcome field must be populated by some code path AND read by some consumer. If you're tempted to write `// reserved for X`, delete it instead and add it back when X actually lands. Reserving names for future implementation introduces drift — a future session either builds UI around a value whose count is always zero (false promise to the user) or removes it as dead code and breaks the supposed future-extension promise; both are bad.
- **Why missed:** Deep-review #5 (EquipPresets, 2026-05-06) flagged a data-flow inconsistency: `SlotApplyOutcome.SlotLocked` was declared in the enum, had a `case` arm in the service switch, and `PresetLoadResult.SkippedLockedSlots` was a result field — but across the whole codebase zero places returned `SlotLocked` (the adapter only returned Equipped / ItemMissing / ModifierMissing / HeroNotFound / Failed) and the `SkippedLockedSlots` list was always empty. Caught via Data Flow Tracing (Agent 5 in the deep-review skill) reporting "X is declared but never returned" / "Y is a field but always empty."
- **Prevent:** Tests are the gate — if you can't write a test that exercises the enum value or field, it's not real yet; delete it. Counter-example that's OK: a future-tense enum value with a producer in the same PR even when the consumer is just a `_logger.LogDebug` line — the producer-consumer chain exists. Sibling rules: `feedback_user_facing_promise_must_match_code.md` (MCM hint vs implementation drift) and `feedback_dont_defer_high_review_findings.md` ("we'll fix it later" silent dismissal); this is the design-time member of that trio.
- **Source:** memory/feedback_no_aspirational_enum_values.md (EquipPresets feature; deep-review #5)

### Check the simplest config causes before deep investigation
When CC/rendering breaks for one race, check XML config references before investigating meshes or engine internals. Order: (1) `skins.xml` for missing action-set references (facegen, warrior, villager, etc.), (2) `monsters.xml` for missing/wrong attributes, (3) `action_sets.xml` for missing action definitions — only after ruling out XML config should you investigate mesh/engine-level causes.
- **Why missed:** The elf race broke the CC parent scene; extensive time was spent investigating body meshes, stitching flags, deform keys, and planning Harmony patches. The actual fix was a missing XML reference — `as_elf_facegen` needed to be added to `skins.xml`. The depth of the cause (engine-level mesh incompatibility) was assumed without first checking the simpler possibilities.
- **Prevent:** For any race-specific rendering issue, walk the XML-config checklist above first and rule it out before escalating to mesh/engine analysis.
- **Source:** memory/feedback_simpler_fix_first.md (character-creation / race rendering)

---

### A log line names the field its author intended, not the field its name suggests

`[CultureMarketplace] town_K1 (battania)` was read as "the settlement's culture is battania" and sent the investigation down a wrong branch — it looked like proof that TAOM's culture conversion had already retagged the town, which would have ruled out the actual root cause. The line is fed by `ITownRosterAdapter.GetCurrentCultureId(Settlement)` → `settlement?.OwnerClan?.Culture?.StringId` — the **owner's** culture. Worse, `ICultureConversionAdapter.GetCurrentCultureId(string)` is a same-named method on a sibling adapter that returns the settlement's *own* culture, so reading either one in isolation confirms whichever reading you started with.
- **Why missed:** the diagnostic was trusted at face value because its name (`GetCurrentCultureId`, printed next to a settlement id) matched the hypothesis under test. A same-named sibling with different semantics made the mistake self-confirming.
- **Prevent:** read the getter before reasoning from a diagnostic — especially when that line is the *only* evidence contradicting a hypothesis. When two adapters expose the same method name, check which one the log call site actually holds.
- **Source:** `docs/reviews/rca-landless-culture-spawn-2026-08-04.md` (#374, landless-culture CTD)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/body-properties.md](../../modding/body-properties.md)
- [docs/modding/troubleshooting.md](../../modding/troubleshooting.md)
- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
### The artifact a reporter attaches may not be the crash they are reporting

**Symptom:** a player reported *"every instance that I attempt to find a female dwarf in a
settlement/battle/tournament, if my camera looks at them it crashes my game, but it will not crash if
I interact with them in dialogue"* and attached a tournament crash bundle from a dwarf campaign. The
bundle and the complaint were **two unrelated defects**:

| | Tournament NRE (#407) | Female dwarves (#403) |
|---|---|---|
| Kind | Managed `NullReferenceException` in a vanilla VM | Native AV `0xC0000005` |
| Evidence | Full crash bundle, clean managed stack | **No bundle at all** |
| Cause | Unguarded `hero.MapFaction.Color` | One unresolved mesh name in `skins.xml` |
| Sex/race relevance | None | Entirely |

**Why missed:** the attachment was treated as evidence *for the stated complaint*. It was evidence for
a different, real bug that happened to be in the same save.

**Prevent:** when a report's symptom and its artifact do not obviously describe the same event, treat
them as two investigations until one is shown to explain the other. And read "no crash bundle" as
positive evidence of a native fault, not as absence of evidence — TAOM's CrashReport finalizers only
see exceptions that cross a managed boundary. Corroborating record:
`investigation-rhun-dwarf-ctd-2026-08-02.md` Established #3.

**Source:** `docs/reviews/rca-patch69-tournament-guard-2026-08-07.md` (#403 / #407).

### Dropping a donor setting drops the behaviour of its DEFAULT value

Porting a donor mod, three separate behaviours were removed as YAGNI because they were expressed as
config knobs nobody wanted to carry: `AllowMultiplePromotions`, a roster precondition, and an
`IsHero` filter on an upgrade walk. In each case the knob was genuinely not worth porting — but the
value it defaulted to WAS the donor's shipped behaviour. Deleting the knob silently deleted the
default. `AllowMultiplePromotions=false` was the only thing capping promotion offers at one per
battle; without it a won battle could raise dozens of consecutive game-pausing modal prompts.

The mirror case, from the same port: FIXING a donor bug can remove an unnamed side effect. The donor
deducted merit when an offer was queued (a real bug — declining destroyed earned merit). Fixing it
correctly also removed the only thing suppressing the re-ask, so the same soldier was proposed after
every won battle forever.

**Prevent:** when dropping a donor setting, write down what its DEFAULT value did and either keep that
behaviour as a constant or record explicitly that you are changing it. When fixing a donor bug,
enumerate what the buggy behaviour was incidentally providing before removing it. "The knob is YAGNI"
and "the behaviour is YAGNI" are different claims and need separate answers.

**Source:** `docs/reviews/rca-field-commission-2026-08-07.md` findings 1, 3, 6, 10.

### Never promote a subagent's factual detail into a durable artifact without running the check yourself
A research subagent reported that Python's `wave` module "misreports IMA ADPCM by roughly 10x". The
surrounding argument was sound and its conclusion was right, so the detail was written verbatim into
a code comment justifying a design choice. It is false: `wave` does not misreport ADPCM, it raises
`wave.Error: unknown format: 17`. The command that settles it is two seconds long. In the same
changeset a docstring asserted "every mp3 in this tree is CBR" (38 carry a Xing/Info VBR frame, and
a VBR clip can measure SHORT, the unsafe direction for a length gate) and a doc table asserted a
`VoiceType` had exactly one call site (a second exists in the character-creation facegen preview).
- **Why missed:** all three claims were specific, checkable, and felt settled, so the seconds were
  not spent. The subagent one is the sharpest: `evidence-over-claims.md` §A.4 already names exactly
  this failure, and a persuasive report made its incidental details feel verified by association.
  A confident subagent report is a claim, not evidence, and its supporting details are separate
  claims from its conclusion: adopting the conclusion does not adopt the reasons.
- **Prevent:** before a subagent-sourced fact goes into a code comment, doc table, CHANGELOG or
  commit message, run the one command that proves it. If the claim is not worth the seconds, it is
  not worth stating: cut it, or attribute it as unverified. Comments that justify a design choice
  are load-bearing precisely because the next reader will not re-derive them.
- **Source:** docs/reviews/rca-dwarf-voices-2026-09-06.md (2026-09-06 deep review)
