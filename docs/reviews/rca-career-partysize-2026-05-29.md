# RCA — Career Passive Party-Size Bug + Dead Wrapped-Schema Passives (2026-05-29)

## Summary

A user-reported bug ("Gondor infantry +2 party size grants ~+150") was root-caused to the career
passive system applying a whole-count `PartySize` magnitude through `ExplainedNumber.AddFactor`
(multiplicative) instead of `Add` (flat) — `AddFactor(2)` triples the base. Auditing the subsystem
uncovered a far larger latent defect: **310 career choices authored in a `<PassiveEffects>` (plural)
wrapper with a `value=` attribute were never parsed** (the parser read only a direct `<PassiveEffect>`
child and only the `magnitude=` attribute), so whole careers across all 16 cultures had completely
dead passives.

Both were fixed surgically (Issue A: `TaomPartySizeModel` → `ApplyFlat`; Issue B: parser reads the
wrapper + `value=` alias; 20 wrapped PartySize entries reconciled to flat counts 4/5/6 + 12-language
propagation). Deep-review (6 agents) confirmed no architecture violations, no API incompatibility,
no ×N regression from activation, and surfaced 5 pre-existing dead-consumer enum types now made
reachable.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `PartySize` passive applied via `AddFactor` on a whole-count magnitude → +2 became +200% | Magnitude-scale ↔ application-method mismatch | The cache (`CareerPassiveService.RefreshCache`) sums only `Magnitude` and discards `Operation`/`IsPercentage`; the "magnitude is a fraction, apply via AddFactor" contract was implicit, and PartySize was authored as whole counts violating it. No test asserted the application semantics for any specific effect type. | Service-level regression test contrasting `ApplyFlat` (102) vs `ApplyFactor` (300) for a PartySize magnitude. Deferred: make the cache `IsPercentage`-aware so flat-vs-factor is data-driven, not per-call-site. |
| 2 | HIGH | 310 `<PassiveEffects>`-wrapped choices were unparsed → dead passives for whole careers | Config-schema ↔ parser mismatch | The content was authored in bulk in a second schema (`<PassiveEffects>`+`value=`) the parser never supported. No test loaded the *real* `taom_career_choices.xml` to assert every authored passive choice parses to a non-null passive. Unit tests only fed hand-written direct-schema XML. | New parser tests for the wrapper + `value=` alias + precedence + empty-wrapper. **Codified memory:** add a "parse-the-real-shipped-config" integration assertion for bulk-authored XML. |
| 3 | LOW | New tooling script wrote BOM as a U+FEFF string literal instead of `b"\xef\xbb\xbf"` | Convention drift (tools/README.md XML I/O) | Moot for the current no-BOM corpus, so it never manifested; the dedicated tooling agent (not the 5 C# agents) caught it. | Fixed in-session. The Step-2c tooling-correctness agent is the right net; it fired correctly. |
| 4 | INFO | 5 `PassiveEffectType` values (`Ammo`, `HorseChargeDamage`, `HorseHealth`, `TroopResistance`, `StealthBonus`) have no consumer; ~56 wrapped choices advertising them are no-ops | Enum coverage / user-facing-promise | Pre-existing — these types had no consumer before this PR (their direct-schema entries were equally dead). Activating the wrapper makes more instances *reachable* but introduces no ×N risk and no regression (no consumer = benign silence). | Documented as a CHANGELOG known-limitation + GitHub issue. Implementing consumers is a separate balance/feature decision, out of scope. |
| 5 | LOW | No negative test for an empty/foreign `<PassiveEffects>` wrapper | Test completeness | The happy-path wrapper test was added but the defensive case wasn't. | Added `LoadChoices_EmptyPassiveEffectsWrapper_YieldsNullPassiveNoThrow`. |
| 6 | MED | 2 root `Health` passives (`black_uruk_captain_root`, `olog_hai_warchief_root`) describe "+6%/+8% health" but `Health` is flat-consumed (`value=30/35` → +30/+35) | User-facing-promise / same flat-vs-% class as #1, different effect type | **Caught by Codex, not the 6-agent deep-review.** Activating the wrapped root passives (Issue B) made the percentage wording a live mismatch. Deep-review's data-flow agent noted `Health` is flat-consumed but cross-checked only the *PartySize* descriptions against magnitudes — it did not extend the description↔magnitude check to *other* flat-consumed types (Health) whose roots were newly activated. | Fixed descriptions → "+30/+35 health" across all layers. Generalisable lesson: when activating dormant config, the description↔value consistency check must cover **every** flat-consumed effect type, not just the one being reconciled. |

## Root-cause pattern

Findings #1 and #2 share a theme: **a configuration value/schema authored against one assumption,
consumed against another, with no test exercising the real artifact.** #1 is value-semantics drift
(whole count vs fraction); #2 is structural-schema drift (wrapper vs direct child). Both shipped
because the test suite only ever fed *synthetic, hand-written* config that happened to match the
parser/consumer's assumptions — it never round-tripped the actual shipped `taom_career_choices.xml`.

**Preventive enforcement implemented:** [`CareerChoicesIntegrationTests`](../../TAOM.Tests/Features/CareerSystem/CareerChoicesIntegrationTests.cs)
loads the REAL `taom_career_choices.xml` and asserts (a) it loads >100 choices, (b) every
`type="Passive"` choice parses to a non-null `PassiveEffect`, (c) no passive falls back to
`PassiveEffectType.Special` (unrecognized `type=`), (d) every `<PassiveEffects>` wrapper has exactly
one child (no silent multi-child drop), (e) every parsed magnitude is finite and non-zero (catches a
malformed `value=` parsing to 0). Test (b) would have failed immediately on the 310 dead wrapped
choices; (d)/(e) were added from the Codex adversarial pass. This makes the structural-schema-drift
class impossible to ship undetected.

**Codex adversarial pass (Finding #6):** the heavyweight Codex review verified all 6 suspects against
source (0 false positives) and caught the Health root description mismatch the 6-agent deep-review
missed — see [codex-adversarial-career-partysize-2026-05-29.md](codex-adversarial-career-partysize-2026-05-29.md).
This is the value of the independent adversarial pass: the deep-review checked description↔magnitude
consistency for the type it was reconciling (PartySize) but not for a *sibling* flat-consumed type
(Health) activated by the same change.
The dead-consumer gap (#4) remains documented (not test-enforced) because 5 types legitimately lack
consumers today; enforcing consumer-coverage would require either implementing them or an allowlist.

## Why each deep-review agent missed #1 and #2 originally

These were found by the *investigation*, not by deep-review (deep-review ran on the fix). But for the
record — why they survived the ORIGINAL feature's review:

- **Standards / Compatibility / Efficiency:** none inspect "does the authored XML actually parse into
  a live effect" — they check code shape and API signatures, both of which were fine. The parser was
  *correct* for the schema it supported; the content used a different schema.
- **Completeness:** checks that test files exist, not that tests load real config. The career tests
  existed and passed — against synthetic XML.
- **Data Flow:** would have caught #2 *if* it had traced "every authored choice → parsed passive →
  consumer." Its enum-coverage check (rule 2) traces declared enum values to consumers, but the
  wrapped entries were invisible because they never parsed into the cache to begin with — the gap was
  *upstream* of the data-flow trace's starting point (the parsed passive).

## Feedback memory to codify

One genuine systemic lesson worth a memory: **bulk-authored config (50 careers, 1310 passive
elements) needs a test that parses the REAL shipped file and asserts every authored entity is
non-trivially consumed — synthetic-XML unit tests cannot catch a schema the content uses but the
parser doesn't.** See `feedback_parse_real_config_in_tests.md`.

The magnitude-scale convention (flat types use `Add`, fractional types use `AddFactor`) is documented
inline in `TaomPartySizeModel` and in the feature doc; the deferred fix (data-driven routing via
`IsPercentage`) is recorded there too.
