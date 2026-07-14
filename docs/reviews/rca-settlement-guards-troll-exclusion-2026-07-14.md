# RCA — SettlementGuards troll exclusion (#346), deep-review 2026-07-14

**Top line:** 5-agent deep-review of the #346 changeset (excluded-race guard scrub) returned **no HIGH findings and no data-flow gaps** (14 flows traced, 0 gaps). Two LOW doc-accuracy findings were fixed in-session; three MEDIUM efficiency observations on pre-existing code were declined with recorded reasoning.

## Findings

| # | Sev | Finding | Category | Why missed | Action |
|---|-----|---------|----------|------------|--------|
| 1 | LOW | Two `ReflectionSiteBindingTests` DataRow source-site refs off by ±2 lines (`:28` → actual `:30` after the same session's 2-line insert shifted `Initialize()` down; `:34` → actual `:33`) | doc-accuracy | Line numbers written from memory of the file layout instead of from fresh `grep -n` output — a micro-instance of evidence-over-claims §C (never state a line number you haven't read this turn) | FIXED — DataRows + `reflection-sites.md` rows corrected from grep output |
| 2 | LOW | Stale "v1.3.15" version strings in `GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs` comments (pre-existing; the file was touched this session and Agent 2 re-verified the signature on v1.4.7) | doc-staleness | Pre-existing text outside the edited region; surfaced only because the compatibility agent re-decompiled | FIXED — comments now reference the re-verified v1.4.7 / "installed engine" |
| 3 | MED ×3 | Pre-existing per-spawn allocations: `FilterBySpawnPoint` list alloc, unconditional debug-string interpolation in `ResolveGuardTroopId`, `SettlementGuardContext` class alloc | efficiency (pre-existing) | Not missed — out of the changeset's scope | DECLINED — pre-existing code on a cold path (~20 small allocations per settlement entry, not per-frame). Rejected per edit-scope discipline (a bug fix doesn't refactor adjacent code) and `simplicity-criterion.md` (tiny win). Revisit only if profiling ever implicates settlement entry. |

## Why the changeset itself came back clean

The fix was planned against the decompiled vanilla pipeline before any code was written (both enforcement points chosen from the full producer enumeration), signatures were verified on the installed DLL before authoring, and the harmony-il lessons file was read first — the `AccessTools.Field`-over-`___`-injection and cached-reflection rules were applied at design time rather than caught at review time. The data-flow agent independently re-derived the same producer set (5 paths) and confirmed all were covered.

## Feedback memories to codify

None. Finding 1 is already covered by `evidence-over-claims.md` §C ("state no line number you have not read this turn") — the rule existed and was under-applied to catalogue rows, not absent. No new systemic pattern; manufacturing a rule here would be noise.

## Residual risk (recorded, not code-fixable)

- The scrub loop lives in the patch body (reflection mechanics can't be unit-tested; the *decision* is service-tested). Verification of the removal mechanics is the owed in-game smoke: Mordor town/castle with a troll-carrying garrison across repeated entries, Gondor pool regression, siege defense still fields trolls.
- `cave_troll` being a registered FaceGen race at runtime is an integration fact (Armory `skins.xml` → engine) not pinnable in unit tests; if the Armory module were absent the exclusion would no-op, but the troll troop couldn't spawn at all in that scenario either.
