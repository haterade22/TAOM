# RCA — MenuLinkColors deep review (2026-07-26)

**Scope:** Patch64 / MenuLinkColors, reviewed before first commit. 5 core agents (standards,
API compatibility, efficiency, completeness, cross-system data flow). Suite 4462 green after fixes.

**Outcome:** 3 confirmed code findings (1 HIGH, 2 MEDIUM) — all fixed in-session. 2 process gaps —
1 fixed, 1 outstanding. 5 efficiency findings examined and rejected with reasons.

The HIGH and one MEDIUM share a root-cause pattern: **an optimisation or an inheritance shortcut
whose correctness depended on a fact nobody verified.** Both were invisible to the test suite
because the tests asserted the shortcut's *behaviour* rather than the property it was supposed to
preserve.

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | `MenuLinkStyleRewriter` memoised the last (input, output) pair keyed on the menu **string**, but the rewrite depends on the linked objects' **culture**. Menu text is byte-identical before and after a culture conversion, and across a load of a different save. A stale faction colour could be returned with no indication. | Cache-key vs dependency mismatch | The memo was written as a performance guard before the call frequency was measured. Its test (`Rewrite_RepeatedIdenticalInput_ResolvesLookupOnlyOnce`) asserted the cache *hits* — i.e. it pinned the bug as a feature. No test varied the lookup's answer across two identical inputs. | Memo deleted (not invalidated). New lesson: cache-key completeness. Two regression tests pin recomputation across a changed culture and a changed brush answer. |
| 2 | MEDIUM | The 21 retinted vanilla `Link.*` fallback styles silently kept vanilla's `TextGlowColor="#111111FF"` dark halo. The XML comment asserted glow would inherit the brush's zeroed `Default`. | Inheritance asymmetry | The claim was verified for the *new* styles (`BrushFactory.cs:560` assigns `style.DefaultStyle = brush.DefaultStyle`) and then generalised to the *redefined* ones without re-checking. `Style.FillFrom` assigns through property setters, which latch `_isTextGlowChanged = true` at clone time — so the redefinition never regains the fallback. | All 81 styles now state glow explicitly. `EveryLinkStyle_StatesItsGlowExplicitly` enforces it. XML comment rewritten to explain the asymmetry rather than assert the wrong half. |
| 3 | MEDIUM | `Regex.Replace(text, ReplaceStyle)` allocated a `MatchEvaluator` delegate on every call (method-group conversion on an instance method is not cached). | Allocation | Genuine but minor; not covered by any rule. | Delegate bound once in the constructor. |
| 4 | — | No GitHub issue for the feature. | Process | TAOM requires an issue created *before* implementation. The work began as a question ("where is this colour controlled?") and became a feature without the gate being applied. | **Outstanding** — see below. |
| 5 | — | `docs/features/menu-link-colors.md` was an orphan (no inbound link from `docs/INDEX.md`). | Process | `tools/lint_docs.py` was not run after adding the doc. | Fixed; linter orphan count 3 → 2 (remaining 2 pre-existing). |

---

## Root-cause pattern: unverified-shortcut correctness

Findings 1 and 2 are the same mistake in two materials.

In both cases a shortcut was taken (cache the result; let the attribute inherit), the shortcut's
correctness rested on a specific claim (the key captures everything the answer depends on; unset
attributes fall back to `Default`), and the claim was **assumed for the general case after being
checked for one instance of it**. Finding 2 is the sharper illustration: the inheritance claim was
literally true — for 60 of the 81 styles — and the 21 exceptions were exactly the group the change
existed to fix.

The generalisable rule is not "don't cache" or "don't inherit". It is: **when a shortcut's
correctness depends on a claim, state the claim and check it against every group the shortcut
covers — not against the first group you looked at.**

The test suite could not catch either one, and the reason is worth naming: both tests asserted the
*mechanism* (the cache hits; the style has a `FontColor`) rather than the *property the mechanism
was supposed to deliver* (the colour matches current game state; the text has no halo). A test
written from the mechanism can only ever confirm the mechanism runs.

---

## Why each agent missed these

| Agent | Why it didn't catch #1 (memo staleness) | Why it didn't catch #2 (glow) |
|---|---|---|
| 1 — Standards | No ADR covers cache-key correctness; the class passed every structural rule (interface, ctor injection, no sealed types). | Brush XML is out of its scope entirely. |
| 2 — API compatibility | Verified signatures, not semantics of TAOM's own state. Correctly out of scope. | Would have needed to model `Style.FillFrom`'s setter side effects — it verified `GetStyle`/`GetBrush` existence, which is what it was asked for. |
| 3 — Efficiency | Actively *praised* the memo (flagged only that its hit-rate was unverified). An efficiency lens treats a cache as a benefit and does not ask whether the key is sound. | Not a performance question. |
| 4 — Completeness | Counted tests and docs; a test that pins a bug still counts as coverage. | Read the feature doc for accuracy but had no way to know the XML comment's claim was false. |
| 5 — Data flow | **Caught it.** Tracing "what does the output depend on" against "what is the cache keyed on" is exactly this agent's job. | **Caught it**, by decompiling `Style.FillFrom` and `Brush.FillFrom` rather than trusting the code comment. |

Agent 5 found both. That is consistent with the skill's own note that it is the highest-value
agent, and worth recording: **it was also the agent that crashed on an API error and had to be
relaunched.** Had the run been accepted as "4 of 5 passed, ship it", both findings would have
shipped. A failed review agent is a blocked review, not a 4/5 pass.

---

## Efficiency findings examined and rejected

Recorded so they are not re-raised. Verified rather than dismissed:

| Finding | Verdict | Reason |
|---|---|---|
| Replace `+` concatenation with `$""` interpolation | **Rejected** | The stated rationale ("guaranteed single allocation") is wrong. On net472 Roslyn lowers both an all-string `+` chain and an all-string interpolation to the same `string.Concat` call. The change is a no-op. |
| Cache `DefinesStyle` results per `Rewrite` call | **Rejected** | The fix costs more than the problem — it allocates a `Dictionary` per call to avoid ~3 `TryGetValue` lookups. |
| Cache `GetCultureId` results | **Rejected** | Same shape as finding #1: it would reintroduce exactly the staleness that was just removed. Actively harmful. |
| Add one-shot logging to the adapters' `catch` blocks | **Rejected** | The API-compatibility agent confirmed neither call path can throw — `MBObjectManager.GetObject` returns null via a `Debug.FailedAssert` path, and both call sites are null-conditioned. The `catch` is a belt-and-braces guard for a path that does not fire; the brush-probe case is already covered downstream by `WarnMissingStyleOnce`. |
| `Substring` allocations when splitting the href | **Rejected** | The agent itself rated it acceptable. Two small strings, a few times per menu open. |

The efficiency agent also asked for verification of the load-bearing claim that the setter is not
per-frame. Verified directly in `GameMenuVM.OnFrameTick` / `IsMenuTextChanged`: the flag is driven
by a **reference** comparison against a cached `TextObject`, which is stable in steady state, and
`IsMenuTextChanged` never reads `_contextText` — so the rewritten string cannot feed back into the
change detection. All efficiency findings stay at their MEDIUM ceiling.

---

## Lessons to codify

Appended to `docs/reviews/lessons/gamemodels-services.md` and `lessons/localization-ui.md`
respectively:

### A cache key must capture everything the cached answer depends on

**Why missed:** the memo was keyed on the input string because that is what the function receives,
not because that is what the answer depends on. The dependency (the linked objects' culture) is
reachable only through the lookup the cache exists to skip. The test asserted the cache hit, which
pinned the defect as intended behaviour.
**Prevent:** before adding a cache, write down what the output depends on and confirm the key
covers all of it. If a dependency is not in the key, the cache needs an invalidation signal — and
if there is no cheap invalidation signal, do not cache. Then ask what the cache is actually saving:
if the guarded path runs once per UI interaction, delete it. A regression test must vary a
*dependency* while holding the *key* constant.
**Source:** MenuLinkColors deep review, 2026-07-26 (HIGH). RCA: this file.

### A GauntletUI style that redefines an INHERITED name does not regain the brush Default

**Why missed:** `BrushFactory.cs:560` assigns `style.DefaultStyle = brush.DefaultStyle` at both
patch sites, which reads like every style falls back to the brush `Default` for unset attributes.
It only holds for style names the base brush did not already define. For inherited names,
`Style.FillFrom` (`Style.cs:564`) assigns through the property setters, each of which latches
`_isXChanged = true` — so the value is baked in before your redefinition is parsed, and
`DefaultStyle` is never consulted again.
**Prevent:** when overriding an inherited style in a `BaseBrush` chain, state **every** attribute
you depend on, even ones you expect to inherit. Do not mix "rely on the fallback" and "override
explicitly" in the same block. Pin it with a test that asserts the attribute is present in the XML.
**Source:** MenuLinkColors deep review, 2026-07-26 (MEDIUM). RCA: this file.

---

## Outstanding

**No GitHub issue exists for this feature.** TAOM requires one created before implementation;
this is a retroactive repair and needs the user's call, since `/issue` creates a public artifact
and is never auto-invoked. Nothing else from this review is unresolved.
