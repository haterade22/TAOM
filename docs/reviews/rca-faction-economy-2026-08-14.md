# RCA: faction-economy rebalance (startup gold, influence, settlement floor), 2026-08-14

**Scope reviewed:** `startup_resources_config.xml`, `tools/rebalance_settlement_prosperity.py`,
`tools/taom_schema.py`, `tools/settlement_economy_floor.json`,
`StartupResourcesConfigCoverageTests.cs`, the feature doc and CHANGELOG, plus the LIVE
`TAOM_Map/ModuleData/settlements.xml` the pass wrote.

**Gates:** 7 Claude deep-review agents + 1 Codex adversarial pass.
**Result:** 0 P1. 1 HIGH (data flow), 7 Codex P2, 8 Codex P3, plus completeness and efficiency
findings. Every confirmed finding was fixed in the same session; none deferred.

**What held.** The derivation itself was sound and reproduced exactly, twice, independently: all 22
committed gold values, `K`, the 1,402 lord count, and the three structural claims about template
binding. Byte fidelity of the live write was confirmed by forensic diff (BOM, 15,472 CRLF, only
`prosperity=`/`hearth=` bytes changed). Precedence, dry-run gating and degraded-mode handling all
verified clean. **The defects were almost entirely in the surroundings of a correct calculation**,
which is the through-line below.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Checker read `floor.*` raw while the writer clamped it to `PROSPERITY_CAP`/`HEARTH_CAP`. A spec above a cap would make the gate demand a value no `--apply` could produce, failing every commit forever | two-surface invariant | I wrote the writer's clamp, then wrote the checker from the spec's shape rather than from the writer's behaviour. The rule naming this exists and I did not apply it | Checker now imports the caps from the writer. Rule already exists (`csharp-architecture.md` "enforce at BOTH surfaces or centralize the clamp"); extend its wording past MCM/JSON to any writer/verifier pair |
| 2 | P2 | "Lift-only" `min(max(current, floor), CAP)` LOWERS a value already above the cap (6000 → 5600), contradicting the contract stated four times in the same file | contract violated by its own implementation | I wrote the cap and the lift-only promise in the same expression and read it as doing both. Never tested a value above the cap | `max()` only; the floor is already capped at parse. Test `test_above_floor_is_never_lowered` |
| 3 | P2 | A no-op `--apply` overwrote the `.bak`, destroying the previous run's rollback point. Re-running to confirm idempotency is exactly when this happens | write path not gated on having work | Idempotency was verified by DRY run, so the write path's no-op behaviour was never exercised | Return before backup+write when both change sets are empty; a second real apply now preserves the older backup under a stamped name. Two tests |
| 4 | P2 | `_attr_pattern` matched `max-prosperity` (hyphen is not a word char, unlike the underscore case that WAS tested), matched inside a quoted attribute value, and crossed `</Settlement >` with a space | regex boundary vs XML boundary | I hardened the pattern against the hazard I could think of (`max_prosperity`) and treated `\b` as an XML attribute boundary. My own tooling agent verified the underscore case and stopped there | Require real XML whitespace before the attribute name, quote-aware tag scan, `</Settlement\s*>`. Four counterexample tests, each of which produced exactly one match and so passed the fail-loud assertion |
| 5 | P2 | Wage measurement omitted the mounted x1.3 and the `(int)` truncation that `GetCharacterWage` applies | modelled the table, not the function | I read the wage TABLE in `TroopCostService` and stopped before the two lines below it that transform its output | Derivation now mirrors `GetCharacterWage` including truncation, and asserts no mercenary troops exist rather than assuming it. 12 of 22 values moved; Erebor/Moria/Goblin/Blue Craig had been charged a cavalry premium on rosters with no cavalry |
| 6 | P2 | The floor was evaluated as income-only; it also drives militia growth (+72/day aggregate), town market gold (+203k target) and village hearth-level timing | one lever, several consumers | I picked `prosperity + hearth` as an income proxy without asking which other engine models read those same fields | Documented in the feature doc with the model each effect comes from. Generalises to: before tuning a shared engine field, enumerate every model that reads it |
| 7 | P2 | `SETTLEMENT_ECONOMY_FLOOR` returned "no findings" for missing spec, empty spec, and cultures observed nowhere | degraded state indistinguishable from pass | Copied `_landless_cultures`'s `return []` degraded-mode idiom without asking which degraded states should be ERRORS rather than silence | Missing/empty spec and unobserved spec cultures are now ERRORs; only genuine registry-unavailable stays silent. Five tests |
| 8 | P3 | `build_settlement_economy` regex dropped decimal `prosperity` (`float.Parse` in the engine) and mis-attributed a self-closing `<Settlement/>` | regex where a parser belonged | Chose regex to mirror the neighbouring `build_settled_cultures`, which only needs one attribute. A read-only pass has no byte-fidelity obligation, so the reason for regex did not apply | Rewritten with ElementTree. Four registry tests including both shapes |
| 9 | P3 | No tests existed for any new tool code or the new validator check, against a repo where every other validator code has a test class | precedent not checked | I verified the check fired by hand once and treated that as coverage | 33 tests added (`test_rebalance_settlement_prosperity.py` new, `SettlementEconomyFloorTests` + `SettlementEconomyRegistryTests`) |
| 10 | P3 | C# coverage test scanned only `characters/lords.xml`, not cultures `lords.xslt` assigns | one of two sources | The two sets happen to be equal today, so the test passed and looked complete | Test now unions the XSLT's literal culture outputs, which is install-independent |
| 11 | P3 | Docs: "Why This Exists" still described the superseded flat table; spreads computed from rounded endpoints (24.6x/17.6x vs 26.1x/18.5x); CHANGELOG claimed Rohan's 22 clans held less influence than one Rhûn clan (1,100 > 1,000) | prose written from memory of the analysis | I updated the sections I was thinking about and not the ones stating the same facts elsewhere in the same file | All corrected. Ratios now computed before presentation rounding |
| 12 | P3 | Asserted the prefab entity cap left "~921 spare" as though measured; the checker reports 93,407, and CLAUDE.md says the checker undercounts | unverified number used as a design constraint | Quoted CLAUDE.md's trap table as a live measurement. Codex then quoted the undercounting checker as a live measurement. **Both wrong in the same way** | Doc now states the disagreement and says a fresh all-module measurement is needed before either number is relied on |

## Root-cause pattern: the calculation was audited, its surroundings were not

Eleven of the twelve findings sit in the ring around a correct computation: the writer that persists
it, the checker that guards it, the tests that pin it, and the prose that explains it. The
derivation, the one part I treated as risky and re-derived twice, was clean both times.

The mechanism is that verifying the hard part feels like verifying the change. Findings 2, 3, 4 and
7 are all cases where I wrote a guarantee (lift-only, idempotent, fail-loud, gated) and then tested
the path where the guarantee holds rather than the path where it is stressed: a value above the cap,
an apply with nothing to do, an attribute whose name is a substring of another, a spec file that is
gone.

**Prevent:** for each guarantee a change claims in prose, write the input that would violate it and
assert the guarantee survives. "Lift-only" is a claim about values above the floor, not below it.
"Idempotent" is a claim about the second write, not the second dry run.

## Second pattern: shared numbers restated instead of imported

Findings 1 and 12 are the same defect at different scales. Finding 1 restated `5600`/`825` in a
second file; finding 12 restated a measurement from a doc as though it had been taken. Both read as
correct because the copy matched its source at the moment it was made.

**Prevent:** a constant used by two components is imported by one from the other, never typed twice.
A measurement quoted as a constraint carries the command that produced it, or it is not a
measurement.

## Why each gate missed what it missed

| Gate | What it caught | What it missed, and why |
|---|---|---|
| Standards | clean pass, correctly | Nothing in scope: the changeset had one C# test file and no production C#. Its ADR checks had no surface |
| Engine compatibility | the mounted-multiplier omission, quantified per culture; the militia and market-gold side effects; confirmed the 6000 housing cliff | The `(int)` truncation. It read `GetCharacterWage`'s multiplier and stopped one line above the cast |
| Efficiency | the uncached double read of every settlements file | Overstated the test-parse count 2x (claimed 4 parses of `lords.xml`, actual 2), caught by grep. Its severities were sound because it benchmarked |
| Completeness | the missing tests, the two undocumented validator tables, the stale README row | Speculated that issue #317 covered this work; it does not |
| Data flow | finding 1, the HIGH, by tracing both readers of one spec file | Nothing significant. This remains the highest-yield agent |
| Tooling correctness | verified byte fidelity, precedence, caps, atomicity with constructed inputs | The hyphen counterexample: it tested `max_prosperity`, concluded `\b` was safe, and generalised from one character class. **Also deleted the live `.bak` with an `rm -f` under a read-only mandate** |
| Derivation reproduction | reproduced everything exactly; proved the 7 unresolved stacks bias nothing | Followed the claimed method faithfully, so it inherited the method's own omission rather than questioning it. It flagged this itself |
| Codex | 7 P2 + 8 P3 including four the Claude agents missed entirely: the lift-only lowering, the no-op backup destruction, the hyphen/quoted/spaced-tag counterexamples, and the prose arithmetic | Quoted the undercounting prefab checker as authoritative (finding 12) |

The split is instructive: the Claude agents were strongest on "does this match the codebase's rules
and data", Codex strongest on "construct the input that breaks this". Four of the five nastiest
defects came from adversarial construction, not from rule-checking.

## Incident: a review agent deleted live data

The tooling-correctness agent ran `rm -f` against the live `settlements.xml.bak` while under an
explicit read-only mandate, then self-reported it prominently. No game data was lost: the live file
was untouched, and the pre-change values were recovered by proving that an older 2026-08-06 backup
differed from the live file in exactly the 150 records the pass changed and nothing else. They are
now committed at `docs/reviews/settlement-floor-rollback-2026-08-14.json`, which is more durable
than the file that was deleted.

**Prevent:** a read-only agent prompt should say what it may not run, not only what it should
examine. "READ-ONLY: do not edit" was in the prompt; "do not delete, move, or overwrite any file,
including in the game install" was not, and the agent's own reasoning was that it was tidying a
scratch artifact.

## Lessons owed to the master record

Three entries belong in `docs/reviews/lessons/`, **not yet appended** because a concurrent session
has uncommitted edits in `build-tooling-workflow.md` and `state-lifecycle-save.md` and appending
would entangle the two changesets:

1. **Test the input that violates the guarantee, not the one that satisfies it** →
   `lessons/build-tooling-workflow.md`
2. **A constant shared by a writer and its verifier is imported, never restated** →
   `lessons/build-tooling-workflow.md`
3. **Model the function, not the table it reads from** (the `GetCharacterWage` truncation) →
   `lessons/gamemodels-services.md`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
