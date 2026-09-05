# RCA — Custom Battle Curated Commander Lists (2026-06-27)

Codex adversarial review of the curated-commander feature (commit `656daae8`). Codex DISPUTED 5 of 6 Known Suspects (implementation held), cross-verified all 43 shipped lord ids as resolvable, and returned **1 HIGH + 1 LOW**. Both confirmed against source and fixed in-session. Prompt + raw review: `docs/reviews/codex-adversarial-custom-battle-lords-2026-06-27.{prompt.md,md}`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | A curated faction whose ids ALL fail to resolve (typo'd JSON / removed lord) stays "curated" at the service layer, `SideCommanderFilter` skips every null, the UI patch early-returns on `count==0`, and the dropdown is left showing the **global unfiltered** commander list — not a fallback to the faction's real lords. | Missing fail-safe (incomplete fallback) | The "fall back to default" fail-safe was implemented only at the PROVIDER/load layer (empty/whitespace ids → faction not registered → default). Syntactically-valid-but-unresolvable ids pass load validation, so `HasCuratedEntry` stays true and the service returns ids that all resolve to null downstream. The end-to-end "what does the dropdown show when the curated list resolves to empty?" question was never traced. | (a) Service now filters curated ids by character existence and falls through to the default per-culture path when none survive (`CustomBattleService.CharacterExists` + curated branch). (b) Shipped-data regression test (`CustomBattleCommandersShippedDataTests`) cross-checks every shipped id against real `lords.xml`/`lords.xslt` — catches the most likely trigger (a typo in the shipped config). (c) LESSONS-LEARNED entry below. |
| 2 | LOW | `docs/features/custom-battles.md` Configuration section still said "No external configuration files" after the JSON config was added. | Documentation drift | Edited the new mechanism into the doc's body but didn't reconcile the older blanket statement lower in the same file. | Updated the Configuration section to describe the JSON + the fallback. No systemic rule. |

## Root-cause pattern (Finding #1)

**A "fall back to default" fail-safe must cover runtime-unresolvable inputs, not just load-time-invalid inputs.** Validation at the config-load boundary (reject empty/dup/unknown) is necessary but not sufficient when the *resolvability* of a value can only be determined later (here, against the live `MBObjectManager`). The design treated "validated at load" as equivalent to "will produce a usable result," but a syntactically-valid id that doesn't resolve produces an *empty* downstream result, and the consumer's empty-result branch (UI early-return) silently fell back to the WRONG state (the global list) instead of the intended default.

This is a sibling of the existing Agent-5 rule **2c (DTO non-empty-output trace)** — "is the field populated?" ≠ "are non-empty values actually produced?" Here the twist is one layer further out: the field (curated id list) IS populated and non-empty, but its *elements* don't resolve, so the *resolved* collection is empty — and nobody traced the consumer's behavior in that state.

## Why each deep-review agent missed it

- **Agent 1 (Standards), 2 (Compat), 3 (Efficiency):** out of scope — this is a behavioral/fail-safe gap, not a standards/API/perf issue.
- **Agent 4 (Completeness):** verified tests exist for the provider's load-time validation rules; there was no test for the runtime all-unresolvable case because the gap wasn't identified.
- **Agent 5 (Data Flow):** closest miss. Flow 4 traced "unresolvable ids are warned + skipped at SideCommanderFilter" and marked it ✅ CONNECTED — treating warn+skip as the terminal behavior. It did not ask the follow-on question: *when every id is skipped, what does the dropdown end up showing?* The 2c rule it carries is about the producer's output being non-empty; the failure here is the producer's output being non-empty but its resolved form being empty, with a wrong consumer fallback. Codex caught it by tracing the UI patch's `count==0` early-return back to the vanilla `RefreshValues` global-list population.

## Lessons codified

Appended to `docs/reviews/LESSONS-LEARNED.md` (GameModels & Services):

> **A "fall back to default" fail-safe must cover runtime-unresolvable inputs, not just load-invalid ones.** When a config value's *validity* is checked at load but its *resolvability* is only knowable later (against the live engine / object manager), trace the consumer's behavior for the case where the value passes load validation but resolves to nothing. An empty resolved result must hit the intended default, not whatever stale/global state the consumer had before. **Why missed:** the fallback was implemented at the load boundary only; the data-flow trace marked "warn + skip unresolvable" as terminal without asking what the consumer shows when everything is skipped. **Prevent:** for any "curated overrides default" feature, add a test for the all-unresolvable case asserting the default path is taken; extend Agent-5 rule 2c reasoning from "is the collection populated?" to "does the collection's *resolved* form produce a usable result, and if not, is the fallback correct?" **Source:** Codex review 2026-06-27, `docs/reviews/rca-custom-battle-lords-2026-06-27.md`.

No new `.claude/rules/` file (the existing csharp-architecture "Config Providers MUST Validate" + Agent-5 2c cover the family; this is a depth extension recorded in LESSONS-LEARNED, not a new rule).

## Verification

- `dotnet build Main/TAOM.csproj` — clean.
- `dotnet test --filter CustomBattles` — 65 passed / 0 failed (+4 over the pre-fix 61: 2 fallback-path tests, 2 shipped-data regression tests).
- `python tools/validate_moduledata.py` — PASS.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
