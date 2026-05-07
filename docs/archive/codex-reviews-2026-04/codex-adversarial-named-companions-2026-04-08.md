# Codex Adversarial Review: Named Companions + Wanderer Race Fix

**Date:** 2026-04-08
**Target:** working tree diff
**Verdict:** needs-attention

No-ship: the data cross-checks passed, but the load-time recovery path can steal legitimate named companions out of play by forcibly reactivating and teleporting any alive companion who is not already in a settlement.

## Known Suspects Verdict

1. **UNUSED FIELD (_logger):** Not explicitly confirmed/disputed in output. Manual check needed.

2. **HERO STATE ON LOAD:** CONFIRMED — See Finding 1. `ChangeState(Active)` on already-Active hero is technically safe (vanilla doesn't guard) but the broader issue is that `EnsureCompanionsPlaced()` teleports any alive companion not currently in a settlement, including recruited ones.

3. **IS_PLACED CHECK:** CONFIRMED — The check `StayingInSettlement != null || CurrentSettlement != null` misses recruited companions traveling on the map. They get forcibly re-placed on load. This is the core bug.

4. **RACE DOUBLE-SET:** Not explicitly confirmed/disputed. Manual check needed.

5. **CONFIG SETTLEMENT IDS:** PASSED — Data cross-checks passed per Codex.

6. **WANDERER RACE FIX:** PASSED — Data cross-checks passed per Codex.

## Findings

### [HIGH] On load, any alive named companion outside a settlement is forcibly reset and teleported

**File:** `NamedCompanionAdapter.cs:21-35`

**TAOM code:** `NamedCompanionBehavior.EnsureCompanionsPlaced()` runs on every game load. Gate is only `IsPlacedInSettlement()` which checks `StayingInSettlement != null || CurrentSettlement != null`. `PlaceInSettlement()` unconditionally calls `hero.ChangeState(Active)` + `EnterSettlementAction.ApplyForCharacterOnly(hero, settlement)`.

**Vanilla code:** `Hero.ChangeState()` does not guard against same-state transitions — always updates bookkeeping. `EnterSettlementAction.ApplyForCharacterOnly()` sets `StayingInSettlement` and applies entry.

**Impact:** Any alive named companion legitimately outside a settlement at load time gets teleported back to their spawn town:
- Player-recruited companion traveling on the map → stolen from player party
- Companion in movement/battle/prisoner state → state corrupted
- Companion recently left settlement → snapped back

**Fix:** Before re-placement, check ownership and state explicitly. Skip heroes with:
- `CompanionOf != null` (recruited by any clan)
- Party membership (`PartyBelongedTo != null`)
- Prisoner/fugitive/traveling state
- Any non-wanderer lifecycle attachment

Add a load-path test simulating a recruited named companion and asserting `EnsureCompanionsPlaced()` does not move them.

## Items That Passed

- **Config cross-reference:** All 18 character_id values, settlement IDs, and race values validated
- **Wanderer race audit:** 30 elf + 10 dg_uruk counts correct, 0 orc in DG section
- **Named companion XML:** Definitions consistent with config

## Items Needing Manual Verification

1. **Unused _logger field** — Grep `NamedCompanionBehavior.cs` for `_logger` usage
2. **Race double-set** — Check if `SetHeroRace()` is redundant with XML `race="elf"`
3. **TryKillCompanion protection** — Verify `HasMet=true` protects named companions from vanilla daily-tick culling

## Recommended Next Steps

1. **Fix load-time recovery predicate** (HIGH) — add `CompanionOf`/party/state checks before re-placement
2. **Add load-path test** — recruited companion should not be moved by `EnsureCompanionsPlaced()`
3. **Manual verify** suspects 1, 4, and TryKillCompanion protection
