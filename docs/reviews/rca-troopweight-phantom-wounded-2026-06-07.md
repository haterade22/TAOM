# RCA — TroopWeight phantom-wounded display bug (2026-06-07)

## Top-line summary

A brand-new campaign showed the player party as "62 troops / 16 wounded" with no battle fought. The wounds were **phantom**: the party genuinely had 46 soldiers, 0 wounded, that *weighed* 62 toward the 23 party-size cap because some were weight-≥2 troops.

Root cause: TAOM's TroopWeight feature Postfix-patches `PartyBase.NumberOfAllMembers` to return a **weighted** member total, but deliberately leaves the sibling getter `PartyBase.NumberOfHealthyMembers` **unweighted** (it feeds gameplay: battle supply, casualty tracking, sacrifice limits). Four vanilla display surfaces derive wounded as `NumberOfAllMembers - NumberOfHealthyMembers`; with only the first term weighted, the weight surplus (62 - 46 = 16) rendered as phantom wounds.

This was a **data-flow miss in the original TroopWeight feature** (a getter was weighted without auditing the consumers that combine it with its unweighted sibling), not a regression. The user reported it; a per-file review at original-author time would not have caught it because, in isolation, the `NumberOfAllMembers` patch is correct and "never touches wounds."

Fix: four display-only Postfix patches (one per surface) rewrite the shown battle-ready / wounded numbers using a weighted (healthy, wounded) split, so `healthy + wounded` equals the weighted member total the panel header already shows. The getters themselves are untouched (weighting `NumberOfHealthyMembers` globally would corrupt casualty math, sacrifice limits, and battle troop supply — see "Rejected approach").

## The originating bug

| # | Sev | Bug | Category | Why it shipped originally |
|---|-----|-----|----------|---------------------------|
| 0 | MED (user-visible, not crashing) | `wounded = NumberOfAllMembers - NumberOfHealthyMembers` manufactures phantom wounds for any party with weight-≥2 troops, on 4 display surfaces | Derived-value / sibling-getter data-flow gap | The TroopWeight feature weighted `NumberOfAllMembers` (correct for party-size budgeting) but did not enumerate the consumers that subtract `NumberOfHealthyMembers` from it. Each consumer is in a *different file* (`CampaignUIHelper`, `GameMenuPartyItemVM`, `PartyBaseHelper`) — a per-file review of the TroopWeight patches never looks at them. |

### Root-cause pattern: weighting one half of a derived-value relationship

`NumberOfHealthyMembers`, `NumberOfAllMembers`, and `NumberOfWoundedTotalMembers` are a derived family: `all = healthy + wounded`. The engine never stores "displayed wounded" — every consumer recomputes it as `all - healthy`. When a mod overrides ONE member of such a family, every consumer that combines it with a sibling silently produces wrong output. The override looks correct in isolation; the breakage only appears at the *combination site*, which lives in unrelated files.

This is the same class as `feedback_review_blindspots.md` ("deep review agents check files in isolation, not data flow") and `feedback_cross_feature_handshake_via_shared_adapter.md` (two features touching shared engine state). The new generalisation: **when you override a getter that participates in a `derived = A op B` engine relationship, grep every call site of the sibling operands and audit each combination, not just the getter you changed.**

## Fix-cycle findings (deep-review on the fix itself)

5 parallel deep-review agents + targeted verification. Standards/compat/completeness all passed. Efficiency + data-flow surfaced 4 items:

| # | Sev | Finding | Category | Why in the first cut | Disposition |
|---|-----|---------|----------|----------------------|-------------|
| 1 | HIGH | `GetWeightedHealthAndWounded` allocated an intermediate `List<(string,int,int)>` per call, on the nameplate hot path (`PartyBaseHelper.GetPartySizeText`, fires per visible party per refresh) | Hot-path allocation | First cut delegated to the pure `ComputeWeightedHealthyAndWounded(IEnumerable<...>)` for testability by materialising the roster into a list. Testability win, allocation cost. | FIXED — extracted a shared `WeightedContribution(weight, number, wounded)` helper used by both the pure (tested) method and a direct roster walk (no list). |
| 2 | MED | No version cache on that O(n) roster walk; the sibling `PartyBaseNumberOfAllMembersHook` caches by `VersionNo` | Missing cache on hot path | First cut prioritised correctness over the nameplate-frequency optimisation. | FIXED — added a `ConditionalWeakTable<PartyBase, box>` cache keyed by `MemberRoster.VersionNo`. Chose `ConditionalWeakTable` over the sibling's `Dictionary<int(hashcode), ...>` because the sibling pattern has a latent unbounded-growth leak (no eviction) + GetHashCode collision risk; the weak table auto-evicts on party GC and keys by reference identity. |
| 3 | MED | Separate-ceiling rounding: `Ceiling(weightedHealthy) + Ceiling(weightedWounded)` can exceed `Ceiling(weightedTotal)` by 1 for fractional weights with mixed wound states, so the tooltip could show battle-ready+wounded 1 above the panel header | Rounding inconsistency | Inherent to ceiling-each-separately. | NOT CHANGED (documented). TAOM ships only integer weights (verified in `troop_weights.xml`), so it never manifests. Matches the existing `PartyVMPopulatePartyListLabelHook` separate-ceiling choice — changing it would make the tooltip disagree with the party-list label. Documented as a known property in code + feature doc. |
| 4 | LOW | Healing-block strip in `RewriteHealthTooltip` had an undocumented contiguous-block assumption | Maintainability | — | FIXED — added a comment documenting the assumption, the fail-safe (skip removal if no healing entry found → leaves the corrected "Wounded: 0" intact), and the RemoveRange bounds-safety argument. |

### Verified-not-a-bug (Suspect 1 from the Codex prompt, confirmed before relying on it)

The new cache keys on `roster.VersionNo` and caches the *wounded* count. If `VersionNo` did not bump when a troop is wounded/healed (only on structural add/remove), the cache would go stale after a battle. Decompiled `TroopRoster.AddToCountsAtIndex` (v1.4.5, line 369-372): `if (countChange != 0 || woundedCountChange != 0) UpdateVersion();`. `WoundTroop` and `WoundNumberOfNonHeroTroopsRandomly` both route through `AddToCountsAtIndex` with `woundedCountChange != 0`, so wounding AND healing bump the version. The cache invalidates correctly. (The sibling `NumberOfAllMembers` cache is immune regardless — it caches a `TotalManCount`-based count that doesn't depend on wounded state — which is why this risk is new to *this* cache, not the existing one.)

## Rejected approach: weight `NumberOfHealthyMembers` globally

The tidy-looking fix — patch `NumberOfHealthyMembers` to be weighted like `NumberOfAllMembers`, so the subtraction is internally consistent everywhere — was rejected. Decompile-verified consumers that would be corrupted: `PartyGroupTroopSupplier` (battle troop supply count), `MapEventParty._healthyManCountAtStart` + `DisorganizedStateCampaignBehavior` (casualty tracking), `DefaultTroopSacrificeModel` (would let you sacrifice more men than you have), `DefaultInventoryCapacityModel`, `DefaultPartyDesertionModel`, battle strength/winner determination. The fix is therefore display-only.

## Why each deep-review agent missed the ORIGINATING bug (#0)

The originating bug was not in *this* changeset (we fixed it), but the lesson is why such bugs survive review:
- **Standards / Compatibility / Efficiency / Completeness agents** are scoped to the changed files. The originating bug lived in the *interaction* between a TAOM patch and three unrelated vanilla files — outside any single feature's file set.
- **Data Flow agent** is the one designed to catch it, and on the FIX it correctly traced all 4 surfaces and confirmed completeness. The generalisable preventive action below extends its remit to derived-value getter families.

## Preventive actions

1. **Memory:** `feedback_weighted_getter_in_derived_family.md` — when overriding a getter that is one operand of an engine `derived = A op B` relationship (esp. the count family `NumberOfAllMembers` / `NumberOfHealthyMembers` / `NumberOfWoundedTotalMembers`), grep every call site of the *other* operands and audit each combination site, which usually lives in unrelated files.
2. **Cache-pattern note:** prefer `ConditionalWeakTable<TKey, TBox>` over `Dictionary<int hashcode, ...>` for per-engine-object caches — reference-keyed (no GetHashCode collision) and auto-evicting (no unbounded growth). The existing TroopWeight count-hook caches should adopt this in a follow-up (latent leak, not urgent).
3. **Doc:** feature doc updated with the phantom-wounded surfaces, the display-only rationale, and the separate-ceiling known property.

## Codex adversarial review

Dispatched 2026-06-07 (gpt-5.5, xhigh). Prompt: `docs/reviews/codex-adversarial-troopweight-phantom-wounded-2026-06-07.prompt.md`. Output: `docs/reviews/codex-adversarial-troopweight-phantom-wounded-2026-06-07.md`. Verdict: 1 CRITICAL / 0 HIGH / 1 MED / 1 LOW. Every finding re-verified against TAOM source + the v1.4.5 decompile before acting (per `evidence-over-claims.md`).

| Codex finding | Sev | My verdict | Action |
|---------------|-----|------------|--------|
| Suspect 4 — `GameMenuPartyItemVM.PartyWoundedSize` setter has a vanilla copy-paste bug (`if (value != _partySize)`); our set order drops the wounded write when desired wounded == current PartySize (e.g. 3 weight-2 troops, 1 wounded → wanted 4/2, got 4/4) | MED→I treat as the real bug | CONFIRMED (verified vs decompile) | FIXED — nudge PartySize off a collision, write wounded, then settle PartySize. `TroopWeightDisplayHook.OnGameMenuPartyItemRefreshCounts`. |
| Suspect 2 — healing-block strip also removes the next section's leading spacer (separator before Prisoners / empty line before Land Troop Capacity) | LOW (cosmetic, values intact) | CONFIRMED | FIXED — preserve the trailing spacer when a real boundary was found. |
| ADDITIONAL — ADR-007: `ITroopWeightService` exposes sealed `PartyBase`/`TroopRoster`/`TroopRosterElement`/`CharacterObject`; service walks `MemberRoster` directly | CRITICAL ("not ready to ship") | DISPUTED as a finding against THIS change | DECLINED w/ reasoning (below) |
| Suspect 1 (VersionNo cache), Suspect 3 (rounding), Suspect 5 (toggle off), Suspect 6 (surface completeness) | — | DISPUTED by Codex = no bug; matches my own analysis | None |

### On the CRITICAL ADR-007 finding — declined with evidence

Codex is technically correct that `TroopWeightService` touches sealed TaleWorlds types. But it FALSELY attributes novelty to this change. The interface ALREADY exposed `PartyBase` (`CalculateWeightedMemberCount`), `TroopRoster` (`CalculateWeightedRosterCount`), `TroopRosterElement` (`CalculateWeightedElementCount`), and `CharacterObject` (`GetTroopWeight`) BEFORE this fix. My additions: `ComputeWeightedHealthyAndWounded(IEnumerable<(string,int,int)>)` — **engine-free** — and `GetWeightedHealthAndWounded(PartyBase)` — uses a type already in the interface. **Zero new sealed types were added.** The deep-review Standards agent independently reached the same conclusion ("consistent with the existing service design; does NOT increase coupling").

Refactoring the whole `TroopWeightService` behind a roster adapter is a legitimate ADR-007 cleanup, but it would touch the four shipped, working count hooks and is out of scope for a phantom-wounded display fix (simplicity-criterion + edit-scope-discipline: every changed line should trace to the user's request). Recorded as a **follow-up**: introduce an `IPartyRosterAdapter` and migrate all of `TroopWeightService`'s sealed-type method signatures (the pure `ComputeWeightedHealthyAndWounded` is already adapter-ready and would be the seam). Not a regression introduced here.

This is the `feedback_codex_caught_api_misread` / `feedback_audit_findings_not_always_correct` pattern in reverse: Codex's verdict (CRITICAL, block ship) over-weights a pre-existing, feature-wide condition. Verified against ground truth, the right call is fix-the-two-real-bugs + document-the-pre-existing-gap, not balloon the scope.

### Why deep-review missed the CONFIRMED Suspect 4

The `GameMenuPartyItemVM.PartyWoundedSize` setter bug is a *vanilla* defect (`value != _partySize` instead of `_partyWoundedSize`) that only bites when our weighted wounded happens to equal the current PartySize. None of the 5 agents decompiled the *setter body* of the VM property they were writing to — they verified the property exists + is public-set (Compatibility agent) but not its guard logic. **Preventive action (memory `feedback_taleworlds_vm_setter_decompile` already exists for exactly this — "decompile the setter BODY before mutating a TaleWorlds VM property post-construction"): this fix is a fresh instance of that rule; the deep-review Compatibility agent prompt should add "for any VM property the patch WRITES, paste the setter body and check its guard," not just confirm the setter is public.** Logged to AGENTS.md + REVIEW-LOG.

**Post-fix verification:** build 0 errors; TroopWeight tests 43/43; full suite re-run below.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/troop-weight-system.md](../features/troop-weight-system.md)

<!-- backlinks-end -->
