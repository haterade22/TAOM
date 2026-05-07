# Codex Adversarial Review: Arena (TaomTournamentModel)

**Date:** 2026-04-06
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship. The Arena override changes tournament cadence, prize selection, and participant armor resolution in ways that materially diverge from Bannerlord vanilla, and the culture-dummy path is provably incomplete for real TAOM cultures.

## Section 1: Vanilla Code

### DefaultTournamentModel (decompiled v1.3.15)

- **GetTournamentStartChance:** Uses `0.1f * eligibleCount - 0.2f` plus a week-of-season hash gate. Filters heroes through `SuitableForTournament(hero)` requiring adulthood and one/two-handed skill > 100.
- **GetTournamentEndChance:** Uses a ramp based on tournament duration.
- **GetRegularRewardItems:** Filters by item value (`regularRewardMinValue` / `regularRewardMaxValue`), merchandise status, reward-eligible categories, player-crafted exclusion. Adds banner rewards, falls back to off-culture items.
- **GetEliteRewardItems:** Uses a fixed curated list of 31 item IDs.
- **GetParticipantArmor:** In tournament mode, uses culture-specific dummy; otherwise returns `participant.RandomBattleEquipment`.

## Section 2: Tournament Math Comparison

### Start Chance

**Vanilla:** `0.1f * eligibleCount - 0.2f` with week-of-season hash gate and `SuitableForTournament` skill filter (one/two-handed > 100).
**TAOM:** Hard switch `0/0.45/0.75/0.90/1.0` based on active adult hero count. No skill filter, no weekly gate.
**Divergence:** Tournaments become near-deterministic in busy towns and trigger off non-combat heroes vanilla would exclude.

### End Chance

**Vanilla:** Duration-based ramp.
**TAOM:** 20-day grace period + 0.033/day linear ramp.
**Divergence:** More predictable tournament lifetimes.

### BuildPrizePool

**TAOM:** Iterates ALL items filtering by culture and tier. O(n) over entire item registry.
**Vanilla:** Uses value-bounded filtering with pre-categorized items.
**Divergence:** TAOM scans wider but unbounded — no value caps on regular rewards.

## Section 3: Culture ID Validation

### Dummy Lookup

`GetParticipantArmor` requests `gear_practice_dummy_{participant.Culture.StringId}`. Repository evidence:
- **Present:** `gondor`, `mordor`, `erebor`, `rivendell`, `lothlorien`, `mirkwood`, `isengard`, `gundabad`, `dolguldur`, plus mapped names (`rohan`, `dunland`, `harad`, `rhun`, `dale`, `khand`)
- **Missing:** `gear_practice_dummy_umbar`, `gear_practice_dummy_vlandia`, `gear_practice_dummy_empire`, `gear_practice_dummy_aserai`, `gear_practice_dummy_khuzait`, `gear_practice_dummy_sturgia`

The tested helper `ResolveDummyId` provides settlement/empire fallback, but **runtime never calls it** — `GetParticipantArmor` bypasses it entirely.

### Fallback

Falls back to `base.GetParticipantArmor(participant)` when dummy not found, which returns `participant.RandomBattleEquipment` — full battle gear instead of intended practice armor.

### Prize Pool Culture Filtering

Custom cultures need items with matching `Culture.StringId` assignment. Coverage for all cultures not verified — some may always get empty pools and fall through to vanilla.

## Findings

### [HIGH] Tournament start math removes vanilla gating and counts ineligible heroes

**File:** `TaomTournamentModel.cs:23-36`

**TAOM code:** Hard switch `0/0.45/0.75/0.90/1.0` counting any active adult hero.

**Vanilla code:** `0.1f * eligibleCount - 0.2f` with `SuitableForTournament` filter (adulthood + combat skill > 100) and week-of-season hash gate.

**Evidence:** Tournaments become near-deterministic in busy towns. Non-combat heroes (merchants, scholars) trigger tournament starts that vanilla would not.

**Remediation:** Reintroduce hero-skill eligibility filter and weekly gate, or add regression tests proving higher frequency is intentional and balanced.

### [HIGH] Prize pool logic discards vanilla value bounds and elite whitelist

**File:** `TaomTournamentModel.cs:45-77`

**TAOM code:** Ignores `regularRewardMinValue`/`regularRewardMaxValue`. Regular: any same-culture weapon/armor in tier band. Elite: every same-culture item with `Tierf >= 4`.

**Vanilla code:** Regular: value-bounded, merchandise-filtered, banner rewards included, off-culture fallback. Elite: fixed curated list of 31 IDs.

**Evidence:** Tournament reward economy is unbounded. Elite pool can include arbitrary high-tier culture gear. No test coverage for model methods.

**Remediation:** Keep vanilla value-window contract for regular rewards. Replace `Tierf >= 4` elite scan with a tested, bounded culture-aware table.

### [HIGH] Culture dummy lookup is incomplete and ResolveDummyId is never called at runtime

**File:** `TaomTournamentModel.cs:80-99`

**TAOM code:** Directly requests `gear_practice_dummy_{participant.Culture.StringId}`. Falls back to `base.GetParticipantArmor`. Tested `ResolveDummyId` helper is never called.

**Evidence:** No `gear_practice_dummy_umbar`, no dummies for vanilla-mapped IDs (`vlandia`, `empire`, `aserai`, `khuzait`, `sturgia`). Umbar participants and all XSLT-culture participants get full battle gear instead of practice armor.

**Remediation:** Use `ResolveDummyId` in `GetParticipantArmor`. Add dummies for `umbar` and all mapped vanilla IDs. Test the actual runtime method, not just the helper constants.

## Observations

- Test file (99 lines) covers constants and `ResolveDummyId` but not `GetTournamentStartChance`, `GetTournamentEndChance`, `GetRegularRewardItems`, `GetEliteRewardItems`, or `GetParticipantArmor`
- `ResolveDummyId` fallback to "gear_practice_dummy_empire" (Dunland) is lore-questionable as a generic default for a LOTR mod
- BuildPrizePool O(n) scan is acceptable for TAOM's item count but less efficient than vanilla's pre-filtered approach

## Recommended Next Steps

1. Wire `ResolveDummyId` into `GetParticipantArmor` and add missing dummy character objects
2. Reintroduce vanilla value bounds for regular rewards or document the intentional change
3. Replace unbounded elite scan with a curated or config-driven table
4. Add model method tests covering start/end chance, reward items, and participant armor
