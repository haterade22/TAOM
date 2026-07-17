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

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
