# Codex Adversarial Review — Career Passive Party-Size Fix (2026-05-29)

Model: gpt-5.5 (xhigh). Scope: commit `991ef7e` (diff `4c68256..991ef7e`). Prompt:
[codex-adversarial-career-partysize-2026-05-29.prompt.md](codex-adversarial-career-partysize-2026-05-29.prompt.md).
(Raw 1.2 MB session trace discarded; this is the verified final review.)

**Verdict: CRITICAL 0 | HIGH 0 | MEDIUM 1 | LOW 0 — ISSUES FOUND (1 fixed).**

## Known Suspects

1. **CONFIRMED clean — flat-vs-factor isolation holds.** `PartySize` only applied via `ApplyFlat` (`TaomPartySizeModel.cs:29`); the other 9 campaign passive types still use `ApplyFactor`. Vanilla `ExplainedNumber`: `Add` flat, `AddFactor` multiplicative.
2. **DISPUTED as an ×N regression — none exists.** No wrapped whole-count passive is consumed via `ApplyFactor`. Wrapped PartySize = `magnitude=4/5/6` + `ApplyFlat`; wrapped Health = flat via `TaomAgentStatCalculateModel.cs:29`; `Ammo` has no consumer (accepted). No `type="PartySize" value=` remains.
3. **CONFIRMED clean — parser precedence documented.** Direct `<PassiveEffect>` wins over wrapper; `magnitude=` wins over `value=` (`CareerConfigProvider.cs:222-235`); `ParseFloat` rejects NaN/Infinity. Real XML: 0 mixed, 0 dual-attr, 0 multi-child.
4. **CONFIRMED test gap (not a data bug) — now closed.** Parser reads only the first wrapped child; the integration test asserted non-null but not single-child. 0/310 wrappers are multi-child today. → Added `RealChoicesXml_EveryPassiveEffectsWrapper_HasExactlyOneChild`.
5. **CONFIRMED clean — `fix_wrapped_partysize_translations.py`** is BOM/byte-faithful, key-scoped, dry-run gated, idempotent.
6. **CONFIRMED for PartySize; found a separate Health text mismatch** (see Finding 1).

## Findings

**[MEDIUM] Health root descriptions advertise a percentage but Health is flat-consumed.** — FIXED.
`black_uruk_captain_root` (`taom_career_choices.xml:588`, `Health value=30`) and `olog_hai_warchief_root` (`:1452`, `value=35`) describe "+6% health" / "+8% health", but `Health` is applied flat (`TaomAgentStatCalculateModel.cs:29` → `baseHealth + magnitude`). These two root passives were dead before the wrapper-parser fix; activating them made the percentage wording a live user-facing-promise mismatch. **Fix:** descriptions → "+30 health" / "+35 health" to match the flat magnitude, propagated to `taom_career_strings.xml` + 12 language files + 11 caches (numeral-token fix, preserving the translated flavor sentence). The "% health" matches on `*_t3_b_p2` are `HealthRegeneration` ("+15% health regeneration") — a separate pre-existing dead-consumer type, out of scope.

## Anything Missed → hardening added

- **Single-child wrapper guard** → `RealChoicesXml_EveryPassiveEffectsWrapper_HasExactlyOneChild`.
- **Finite/non-zero magnitude guard** (malformed `value=` parses to 0, passes non-null check) → `RealChoicesXml_EveryParsedPassive_HasFiniteNonZeroMagnitude`.

## Outcome

1 MEDIUM fixed + 2 hardening tests added. Full suite 2635 passed. Codex performed accurately this pass — all 6 suspects verified against source, no false positives, and it caught a real description/consumer mismatch the 6-agent deep-review missed (deep-review noted Health was flat-consumed but did not cross-check the root *descriptions* against the flat magnitude).
