# RCA — Co-op interop layer (BannerlordTogether coexistence), 2026-07-31

**Scope:** the 31-file changeset adding `CoopPresence`, the PatchShield unpatch gate, the SaveShield
save-load rethrow, the save-definer preflight, the Harmony census, load-order pins, and two
`MBRandom` moves. Reviewed by 7 parallel dimensions (standards, engine API, efficiency, completeness,
cross-system data flow, plus focused passes on the shield behavioural delta and on lifecycle/wiring),
with every deduped finding put to three adversarial verifiers (correctness / reproduction /
simplicity-criterion) that defaulted to refuting, then a completeness critic.

**Result:** 31 raw → 29 unique → **16 confirmed + 2 from the critic, 13 refuted**. No HIGH. Every
confirmed finding was independently re-verified by me against the installed v1.4.7 engine or the
vendored sources before being implemented. All fixed in-session. Suite 4617 green (was 4614; +4 new
tests, −1 deleted tautology).

**Two findings landed on both the confirmed and refuted lists** (same file:line, different category,
so the dedupe key didn't merge them and the two instances drew different verifier panels). I resolved
both by decompiling the engine myself rather than siding with the more confident panel — both turned
out to be real. That is a dedupe-key defect in the review harness, recorded below.

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | Save-definer preflight wired into `OnBeforeInitialModuleScreenSetAsRoot`, which runs *after* `SaveManager.InitializeGlobalDefinitionContext()` — so on the one boot where a collision exists, the engine has already thrown and the preflight never runs | engine-lifecycle-ordering | I picked the hook by analogy with `Patch55_BasicTableauRaceGuard` ("early, before the initial screen") without decompiling `Module.Initialize` to find where the definition context is actually built. Plausible-by-analogy, unverified | Moved to `OnSubModuleLoad`. Rule below: a preflight's hook must be chosen by locating the engine call it precedes, never by analogy |
| 2 | MED | `PatchShield` and `SaveShield` both attach finalizers to the same save-chain methods; PatchShield's unconditional trinity swallow overrides SaveShield's new co-op rethrow, so a `MissingMethod`/`MissingField`/`TypeLoad` during load silently continues on a partial campaign | cross-component-interaction | Both shields were reviewed as units. Nobody asked what happens when two TAOM finalizers land on one method and Harmony's wrapper shares a single exception slot. The changeset *introduced* the interaction by making one shield's return value conditional | `PatchShield.Install` now skips methods `SaveShield.IsShielding` reports. Rule below |
| 3 | MED | Census "contested" section counted `PatchShield`'s own finalizer as a second owner, so every foreign-patched method matched — the section saturated its 60-row cap with noise | diagnostics-noise | I wrote `IsTaomOwner` to answer "is this TAOM?" and used it for "are all owners TAOM?", never asking whether TAOM's *blanket shield* should be a party to a feature-level conflict at all. The shield is TAOM-owned, so the all-TAOM escape hatch never fired | Infrastructure owners are now removed before the comparison, not filtered after. Cap hoisted above the per-method LINQ |
| 4 | MED | Protected-owner prefix `"Bannerlord.UIExtenderEx"` matches neither id UIExtenderEx registers (`bannerlord.uiextender.ex`, `bannerlord.uiextender.ex.viewmodels.<module>`) — PatchShield's rescue path could strip TAOM's own UI mixins | correctness | **Pre-existing** (2026-05-27), inherited verbatim when I extracted the list into `PatchShieldPolicy`. Its doc comment claims it "mirrors the vendored DLLs", which reads as verification and is not. No test exercised a UIExtenderEx id — the one allowlist test used a ButterLib id, which does match | Added the real prefix + a test asserting both ids. Rule below: an allowlist entry claiming to mirror a dependency must be pinned by a test using that dependency's actual value |
| 5 | MED | `<DependedModuleMetadata id="BannerlordTogether" order="LoadAfterThis"/>` in Main's manifest establishes nothing — `ModuleInfo.LoadWithFullPath` parses `DependedModules`, `ModulesToLoadAfterThis`, `IncompatibleModules`, `SubModules` and has no branch for `DependedModuleMetadatas` | manifest | I copied the metadata idiom from TAOM's existing Native/SandBoxCore rows, which *work* — but only because those are also declared in `<DependedModules>`. The idiom carried an implication it doesn't have on its own | Added the engine-honoured `<ModulesToLoadAfterThis>`; drift test now asserts both manifests |
| 6 | MED | Feature doc claimed TAOM version parity "enforced by the build stamp in save metadata"; the stamp is write-and-log only, and the same doc's Known Limitations table said the handshake "is designed but not built" | doc-accuracy | I wrote the Requirements table from the design intent and the Limitations table from the code, in one sitting, and never diffed them against each other | Corrected. Rule below: a doc's promise table and its limitations table must be read against each other before shipping |
| 7 | LOW | Doc claimed "a unit test pins that boundary" for the no-decompile guarantee; the test pins the census *model's* property names and cannot see what `HarmonyCensusWriter` reads | doc-accuracy | Same sitting as #6. The test is real and useful; the sentence overstated its reach | Softened to what is true. On a licence-driven guarantee this was the one overclaim with a real cost |
| 8 | LOW | `CoopPresence.MarkConstructionFailed` had zero callers, and `Refresh()` would have re-added the id from the launcher list even if wired — a documented safety valve that did not exist | dead-code | Written speculatively while designing the probe; the doc comment asserted the guarantee before the wiring existed, and nothing failed when the wiring never arrived | Deleted, and the residual gap written into the doc's Known Limitations as a real, named limitation. `simplicity-criterion.md`: deletion that holds parity always wins |
| 9 | LOW | Census read 4 of the 6 patch collections in Lib.Harmony 2.4.2 — an owner applying only inner prefixes/postfixes is invisible, which the census's own doc tells the reader to interpret as "two 0Harmony instances loaded" | engine-api-compat | I modelled the census on `HarmonyCorrelationCollector`, which also reads four, and inherited its assumption. Harmony 2.4.x added `InnerPrefixes`/`InnerPostfixes`; neither file was re-checked against the pinned version | Added both. `HarmonyCorrelationCollector` has the same gap — filed as follow-up, not fixed here (out of scope) |
| 10 | LOW | Preflight had no one-shot guard while `OnBeforeInitialModuleScreenSetAsRoot` fires on every return to the main menu | lifecycle | Same root cause as #1 — I did not establish the hook's re-entrancy. Notably, the `_basicTableauGuardApplied` one-shot sat 18 lines above the code I added, for exactly this reason | Dissolved by #1's move: `OnSubModuleLoad` is once per process |
| 11 | LOW | `_unpatched` was recorded before the co-op gate, so `UnpatchedCount` and the session summary reported withheld targets as unpatched | diagnostics-accuracy | I placed the gate downstream of the existing dedupe rather than asking what the dedupe set *means* once there are two outcomes | Split into `_unpatched` / `_withheld` with a distinct summary clause |
| 12 | LOW | With a rethrow now reaching them, all five nested SaveShield targets re-attribute the same exception from a Harmony-reset stack, appending junk `(unknown)` culprit rows to the catalog | correctness | A regression my change introduced: before, the rethrow path returned early and did nothing. I reasoned about the finalizer in isolation, not about one exception traversing five shielded frames | Attribute-once via `Exception.Data`. Rule below |
| 13 | LOW | `TaomKnownBaseIds` + its "mutually distinct" test was a tautology over a hand-maintained literal; a fifth colliding definer would leave it green | test-coverage | I wrote the test to feel like a regression guard without asking what change would make it fail. A real reflection-based test already existed in another feature's suite | Deleted both. Rule below |
| 14 | LOW | The `MaxDetailRows` cap and its suppression line were untested, unlike the sibling cap in `CoopModuleList` which has an explicit test | test-coverage | Inconsistent application of my own convention within one changeset | Added the test (which immediately caught a bad assertion predicate of mine, not a code bug) |
| 15 | LOW | CHANGELOG heading omitted `(#370)` | convention | The issue was created after the CHANGELOG entry was written | Added |

---

## Root-cause patterns

### A. Choosing a lifecycle hook by analogy instead of by locating the engine call (#1, #10)

Both defects are one mistake. I needed the preflight to run before the engine builds its save
definition context, and I picked the hook because a neighbouring guard used it — never decompiling
`Module.Initialize` to find where `InitializeGlobalDefinitionContext()` actually sits (line 285,
after `LoadSubModules` at 267 and long before `OnBeforeInitialModuleScreenSetAsRoot` at 758). The
result was a guard that could only ever run on boots where it had nothing to report.

This is the same shape as the **apply-timing** defect the deep-review skill already documents for
Harmony categories (issue #299: a Save/Load CTD guard registered in the campaign-init batch, so the
prefix attached after the cold menu screen had already rendered). That rule is scoped to
`_harmony.PatchCategory` calls. It did not fire here because this is not a patch — but the failure is
identical: *registered ≠ ran in time*.

### B. Two TAOM components landing on one engine method (#2)

`PatchShield` and `SaveShield` were each correct alone. The changeset made one of them return a
conditional value, and nobody asked what the *other* one does to that value on the methods they
share. Harmony runs every finalizer on a method against one shared exception slot; last non-void
return wins. TAOM now owns five distinct Harmony ids, so this is a class, not an incident.

### C. Writing the guarantee before the wiring, then never re-reading the guarantee (#6, #7, #8)

Three findings, one habit: a doc comment or doc table stating what the code is *meant* to do,
committed in the same pass as code that doesn't yet do it. In #8 the XML doc asserted a safety
property with no call site; in #6 and #7 the feature doc's promise outran its own limitations table
and its own test. Each is individually small; together they mean the artifact most likely to be
trusted by a future session was the least accurate thing in the changeset.

### D. Tests written to look like guards (#13, #14)

`Detect_TaomOwnDefinerBaseIds_AreMutuallyDistinct` asserted distinctness of a literal array — no edit
to the real definers could make it fail. Its own comment claimed it guarded "someone adding a fifth
definer by copy-paste", the exact scenario it cannot see. The test to write is the one that fails on
the change you fear.

---

## Why each review dimension missed what it missed

- **standards** returned clean and was right to: nothing here is an ADR breach. It correctly declined
  to flag the two-types-per-file and direct-`new` questions after checking repo precedent rather than
  applying a generic rule — the right call, and worth noting because the cheap failure mode for that
  dimension is inventing violations.
- **api** found #1 and #9 — both by decompiling rather than reasoning. It was the only dimension that
  opened `Module.Initialize`. That is why #1 was found at all.
- **efficiency** found #3 and #10 by asking "how often does this run", which is the question that
  exposes a mis-chosen lifecycle hook from the other direction.
- **completeness** found #6, #14, #15 — the doc/test-hygiene cluster — because it read the doc against
  the code instead of reading the doc.
- **dataflow** found #4, #8, #11, #13 by tracing declarations to consumers; #4 in particular required
  decompiling a *vendored dependency* to learn what string it registers, which no per-file review
  would reach.
- **shield-behavior** (focused) found #12 by tracing one exception through five nested frames — a
  question only askable if you scope an agent to the behavioural delta rather than to files.
- **lifecycle-wiring** (focused) found #5 by parsing the engine's manifest reader instead of trusting
  the XSD the file references.
- **the completeness critic** found #2, the single sharpest finding, precisely because its brief was
  "what did nobody ask about" — the seven dimensions each reviewed a component, and #2 lives between
  two components.

The structural lesson: **#2 and #5 were found by the two agents whose briefs were "look between the
pieces" and "verify the mechanism, not the idiom".** A per-file review of any depth would have missed
both.

---

## Harness defect found by this review

The dedupe key was `file:line:category`. Findings #5 and #11 were each filed twice by different
dimensions under different category strings, so they did not merge — and the two copies drew
independent verifier panels that reached **opposite verdicts**. Had I taken the panels at face value I
would have implemented one instance and discarded the other. Fix for the next run: dedupe on
`file:line` alone and merge categories, or run a reconciliation stage over findings sharing a
file:line before verification. Recorded here rather than in a lessons file because it is a property of
the review script, not of TAOM.

---

## Lessons to codify

Appended to `docs/reviews/lessons/` under the matching categories:

1. **`state-lifecycle-save.md`** — choose a lifecycle hook by locating the engine call it must
   precede, not by copying a neighbour. Extends the existing Harmony apply-timing rule from
   `PatchCategory` calls to *any* startup work with an ordering requirement.
2. **`harmony-il.md`** — when two TAOM components can attach finalizers to one method, one must yield.
   Harmony shares a single exception slot across finalizers and the last non-void return wins.
3. **`testing-qa.md`** — a guard test must be able to fail on the change it claims to guard against;
   an allowlist entry claiming to mirror a dependency must be pinned by a test using that
   dependency's real value.
4. **`build-tooling-workflow.md`** — `<DependedModuleMetadatas>` is a BUTR/BLSE launcher extension the
   vanilla engine never parses; the engine-honoured ordering elements are `<DependedModules>` and
   `<ModulesToLoadAfterThis>`.

No new feedback-memory file: these are subsystem rules, and the lessons files are the canonical record
for those.

---

## Follow-ups not fixed here (out of scope for this changeset)

- `HarmonyCorrelationCollector` (`Main/Features/CrashReport/Collectors/`) has the same 4-of-6 patch
  collection gap as finding #9, so crash-report Harmony correlation under-reports inner-patch owners.
- `CoopPresence` has no unit tests. It is static and does file + reflection I/O, so testing it needs a
  seam (an injectable module-list source). Flagged by a dimension, refuted by the verifiers as
  speculative-for-now; worth revisiting if the probe grows logic.
- The construction-failure gap behind finding #8 is now a documented limitation rather than a fix.
