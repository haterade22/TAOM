# Codex Adversarial Review: SettlementGuards

**Date:** 2026-04-08
**Target:** working tree diff
**Verdict:** needs-attention

No-ship: the patch cannot recover the actual spawn point from vanilla's private method signature, so the XML spawn-point routing is already wrong in both directions, and several spear mappings are keyed to culture IDs that never reach `GetSuitableSpear` at runtime.

## Known Suspects Verdict

1. **REFLECTION CALL ON PrepareGuardAgentDataFromGarrison:** Not explicitly confirmed/disputed in output — Codex focused on higher-severity spawn-point issue. Manual verification needed.

2. **DEAD HarmonyPatchCategory ATTRIBUTE:** Not explicitly confirmed/disputed. Manual verification needed.

3. **SPAWN POINT TAG AMBIGUITY:** CONFIRMED — See Finding 1. Vanilla passes only `(culture, overrideWeaponWithSpear, unarmed)` into `TakeGuardAgentDataFromGarrisonTroopList`. The patch cannot distinguish `sp_guard_castle` from `sp_guard_with_spear` (both pass `overrideWeaponWithSpear: true`). `GetSpawnPointTag` collapses both to `null`.

4. **TROOP ID VALIDITY:** Not explicitly cross-referenced in output. Manual verification needed against `troops_gondor.xml`.

5. **SETTLEMENT ID VALIDITY:** Not explicitly cross-referenced in output. Manual verification needed against `settlements.xml`.

6. **CULTURE ID vs SETTLEMENT CULTURE:** CONFIRMED — See Finding 2. Spear mappings use lore IDs (`rohan`, `dunland`, `harad`, `rhun`, `dale`, `khand`) but runtime passes engine culture IDs (`vlandia`, `empire`, `aserai`, `khuzait`, `sturgia`, `battania`). Six mappings are dead at runtime.

## Findings

### [HIGH] Spawn-point-specific guard config is lossy and misroutes guards

**File:** `GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs:45-95`

**Vanilla code:** `AddGuardsFromGarrison` fans out into five spawn tags:
- `CreateCastleGuard(... TakeGuardAgentData(culture, overrideWeaponWithSpear: true))`
- `CreateStandGuard(... TakeGuardAgentData(culture))`
- `CreateStandGuardWithSpear(... TakeGuardAgentData(culture, overrideWeaponWithSpear: true))`
- `CreatePatrollingGuard(... TakeGuardAgentData(culture))`

But `TakeGuardAgentDataFromGarrisonTroopList` only receives `(CultureObject culture, bool overrideWeaponWithSpear, bool unarmed)` — no spawn tag.

**TAOM code:** `GetSpawnPointTag` (lines 87-95) tries to reconstruct the tag from booleans but:
- `overrideWeaponWithSpear=true` → returns `null` (collapses castle + spear spawns)
- `overrideWeaponWithSpear=false` → returns `"sp_guard"` (collapses stand + patrol spawns)

**Impact:**
- `gondor_mt_captain` tagged `sp_guard_castle` can appear at `sp_guard_with_spear` positions
- `gondor_osg_archer` tagged `sp_guard_patrol` becomes unreachable (patrol looked up as `sp_guard`)
- The core per-spawn-point customization contract is false under real runtime calls

**Fix:** Don't infer spawn tags from booleans. Either patch a higher-level method that knows the actual tag, or carry caller context explicitly (e.g., thread-local or per-call state around each `Create*Guard` entry point).

### [MEDIUM] Six culture spear mappings are unreachable — lore IDs vs engine culture IDs

**File:** `settlement_guards_config.xml:151-164`

**Evidence:** Config maps:
- `rohan` → runtime uses `vlandia`
- `dunland` → runtime uses `empire`
- `harad` → runtime uses `aserai`
- `rhun` → runtime uses `khuzait`
- `dale` → runtime uses `sturgia`
- `khand` → runtime uses `battania`

`SettlementGuardService.ResolveSpearItemId` is a plain dictionary lookup with no alias translation. Guard NPCs use engine culture IDs (e.g., `guard_rohan` has `culture="Culture.vlandia"`).

**Impact:** These 6 mappings are dead at runtime. Dunland's `northern_spear_2_t3` mapping silently falls back to vanilla's `western_spear_3_t3`. Other lore-keyed entries add config drift.

**Fix:** Key the XML to runtime culture IDs (`vlandia`, `empire`, `aserai`, `khuzait`, `sturgia`, `battania`) or add alias normalization in `ResolveSpearItemId`. Add integration test exercising `GetSuitableSpear` with real guard character cultures.

## Items Needing Manual Verification

The following suspects were not fully resolved by Codex:

1. **Reflection caching** — Is `AccessTools.Method` for `PrepareGuardAgentDataFromGarrison` cached or re-resolved per call?
2. **Double-patch risk** — `[HarmonyPatchCategory("Patch28_SettlementGuards")]` attribute + manual `_harmony.Patch()` in SubModule.cs
3. **Troop ID validity** — 13 Gondor troop IDs need cross-reference against `troops_gondor.xml`
4. **Settlement ID validity** — 14+ settlement IDs need cross-reference against `settlements.xml`

## Recommended Next Steps

1. **Fix spawn-point routing** (HIGH) — patch higher-level method or carry caller context
2. **Fix spear culture IDs** (MEDIUM) — use engine IDs or add alias normalization
3. **Manual cross-reference** — verify troop IDs and settlement IDs against XML data files
4. **Verify reflection caching** — check if `AccessTools.Method` result is stored in a static field
5. **Verify patch registration** — confirm no double-patching from attribute + manual patch
