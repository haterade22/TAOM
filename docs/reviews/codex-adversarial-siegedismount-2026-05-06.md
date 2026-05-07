# Codex Adversarial Review — SiegeDismount

**Target:** working tree diff
**Verdict:** needs-attention
**Date:** 2026-05-06
**Output recovery note:** Codex could not write this file directly (`apply_patch` rejected by read-only sandbox); the verdict and findings below are reconstructed from Codex's stdout. `ilspycmd` and `dotnet` were also rejected by shell policy during the run, so vanilla decompilation code blocks could not be produced inline — Claude verified the relevant signatures separately via direct `ilspycmd` invocation outside the Codex sandbox.

## Summary (verbatim from Codex)

> No-ship: SiegeDismount still has a high-confidence false-positive path that can mutate mounts in non-siege settlement-center missions.

## Findings

### [HIGH] Scene-name fallback still matches non-siege settlement center scenes

**Location:** [Main/Features/SiegeDismount/SiegeDismountService.cs:145-148](../../Main/Features/SiegeDismount/SiegeDismountService.cs)

`SceneSiegeKeywords` includes `siege`, and the required all-ModuleData XML grep found many `Main/_Module/ModuleData/settlements.xml` `Location id="center"` rows using scene names like `empire_siege_001`, `khuzait_castle_siege_001`, and `sturgia_castle_siege_001`. In a normal settlement-center mission where `Mission.IsSiegeBattle` is false, this substring fallback still returns true and can clear/deposit the player's mount during a non-siege visit.

**Recommendation:** Remove the bare scene-name substring fallback, or pass stronger mission/encounter context from `SiegeDismountMissionBehavior` and only allow fallback for known custom siege mission contexts.

**Claude verdict:** CONFIRMED. Verified via `Grep scene_name="[^"]*siege[^"]*" ModuleData/` — 24 occurrences across `settlements.xml`. Prior `/deep-review` pass narrowed the keyword list from 5 to 3 but failed to remove `siege` because I assumed those scenes only loaded during real sieges. Codex challenged that assumption and was right: `Location id="center"` scenes can be loaded as cinematic/story Mission contexts where `IsSiegeBattle=false`.

**Fix applied:** Removed the keyword fallback entirely. `IsSiegeMission` now returns `isSiegeBattle` directly. Modded sieges that fail to set the engine flag will not trigger SiegeDismount — documented as a requirement in the feature doc. Tests rewritten: removed the 5-row scene-keyword data test and the 4-row TAOM-castle false-positive regression test (no longer needed); replaced with a 9-row `OnMissionStart_NotIsSiegeBattle_DoesNotTriggerRegardlessOfSceneName` that pins the new contract against vanilla and TAOM scene names.

### [HIGH] Auto-remount discards horse and harness modifiers

**Location:** [Main/Adapters/PlayerMountAdapter.cs:26-55](../../Main/Adapters/PlayerMountAdapter.cs), [Main/Adapters/PartyMountInventoryAdapter.cs](../../Main/Adapters/PartyMountInventoryAdapter.cs)

Capture stores only `MountItemId`/`HarnessItemId`, deposit uses base `ItemObject`, and restore creates `new EquipmentElement(item)`. That means any modified horse or harness becomes an unmodified item after `DismountToInventory` or default `AutoRemountAfter`, which is persistent equipment data loss even though the feature documents it as a limitation.

**Recommendation:** Make the snapshot adapter-owned and preserve the exact equipment element/modifier data across capture, inventory deposit/withdraw, and restore; use modifier-aware inventory APIs if available.

**Claude verdict:** CONFIRMED. Verified via `ilspycmd` against installed v1.3.15 `TaleWorlds.CampaignSystem.dll`: the `ItemRoster.AddToCounts(EquipmentElement, int)` overload exists and preserves the modifier (the `(ItemObject, int)` overload internally calls `AddToCounts(new EquipmentElement(item), number)` — drops the modifier). I had documented this as a known limitation in the doc instead of using the right overload.

**Fix applied:**
- `MountSnapshot` now holds `EquipmentElement Mount` and `EquipmentElement Harness` (internal — TaleWorlds types stay inside the implementation; service still sees only `IMountSnapshot.HasMount/HasHarness/MountItemId/HarnessItemId`).
- New production constructor `MountSnapshot(EquipmentElement, EquipmentElement)` used by `PlayerMountAdapter.Capture()`.
- Old test constructor `(string, string)` retained for unit tests (mocks don't exercise the real round trip).
- `PartyMountInventoryAdapter.Deposit/Withdraw` switched to the `AddToCounts(EquipmentElement, int)` overload via concrete-type cast.
- `PlayerMountAdapter.Restore` writes back the captured `EquipmentElement` directly — modifier survives.

### [MEDIUM] DismountKeepOnMap does not actually dismount

**Location:** [Main/Features/SiegeDismount/SiegeDismountService.cs:75-79](../../Main/Features/SiegeDismount/SiegeDismountService.cs)

For `DismountKeepOnMap`, the service captures a snapshot and sets `_pendingRemount = false`, but it does not clear the horse slots and does not call any mission/agent dismount path. The MCM hint says this mode leaves the player on foot, so mode 1 is effectively a user-visible no-op for the feature's core promise.

**Recommendation:** Implement an actual dismount side effect for mode 1, or remove/rename the mode so the setting does not promise behavior the code never performs.

**Claude verdict:** CONFIRMED. The original developer's decompiled module had the same bug — `case SiegeMountBehaviorType.DismountKeepOnMap: Log("Mount will spawn on map but player will be on foot."); break;` — verbatim no-op. I ported it without challenging the case.

**Fix applied:** Documented honestly. Mode 1 now logs `LogWarning` explaining mode 1 is "Reserved" / equivalent to Vanilla until somebody implements the actual map-side horse spawn (which requires a different API surface — `Mission.SpawnAgent` or similar rather than just clearing the equipment slot). MCM hint updated to "(currently equivalent to Vanilla — full implementation deferred)" so the user-facing promise matches reality. Enum value retained for save-compat. Tests updated to assert the new no-op + warning behavior.

## Things Codex did particularly well

1. **Caught the scene-name false-positive that the prior `/deep-review` Agent 5 missed.** Agent 5 found two TAOM-specific castle false positives (`gate`/`wall`); Codex verified the same class extends to 24 vanilla settlements via `siege` substring. The /deep-review fix was incomplete; Codex closed the gap.
2. **Verified the modifier-aware overload exists.** Agent 5 documented modifier loss as a "known limitation" without checking. Codex pointed out the right `AddToCounts` overload exists, turning a "deferred to follow-up" item into an immediate fix.
3. **Flagged the inherited-bug pattern in mode 1.** Caught that the implementation didn't match the user-visible promise — the kind of bug Claude is trained to overlook ("the original developer tested it, it must be intentional").

## Things Codex did less well

1. **Could not write the output file due to sandbox.** Required Claude to reconstruct the review from stdout. Not Codex's fault — the runtime's `apply_patch` policy is strict; this is a tooling quirk.
2. **Could not run `ilspycmd` due to shell policy.** Vanilla decompilation code blocks were not produced. Claude verified the relevant signatures separately. Future Codex prompts should provide pre-decompiled stubs as code blocks if vanilla verification is critical.
3. **Did not engage with the Known Suspects section's confirm/dispute format.** Codex reported its own findings instead of running the CONFIRMED/DISPUTED loop on the prior /deep-review fixes. Findings 1 and 3 in particular were related to /deep-review's incomplete fixes — the Known Suspects framing would have caught them earlier.

## Root Cause Analysis (Phase 3e)

| # | Bug | Category | Why missed | Preventive action |
|---|-----|----------|-----------|------------------|
| 1 | Scene-name keyword fallback matched 24 vanilla siege center scenes | Logic error / Convention inconsistency | Narrowed the keyword list during /deep-review but didn't remove the keyword fallback entirely. Assumed `Location id="center"` scenes were "only loaded during real sieges" without verifying. | Added [DataTestMethod] regression rows pinning the new contract against vanilla scene names (`empire_siege_001`, `khuzait_castle_siege_001`, `sturgia_castle_siege_001`). Future feature ports that interpret scene names should grep across ALL `ModuleData/*.xml` for substring overlap, not just feature-specific custom XML. |
| 2 | ItemModifier loss on capture/deposit/restore round trip | Missing modifier-aware API | Used `ItemRoster.AddToCounts(ItemObject, int)` overload which drops modifier. Did not verify whether a modifier-preserving overload existed. Documented as a "known limitation" instead of fixing it. | Added a `csharp-architecture.md` lesson: when the adapter touches an inventory or equipment slot that vanilla treats as `EquipmentElement`-shaped (with modifier), use the `EquipmentElement` overload — not the `ItemObject` overload. Search the API surface for both before settling on one. |
| 3 | `DismountKeepOnMap` was a silent no-op despite MCM hint promising "horse on map, player on foot" | Convention inconsistency / Inherited bug | Ported the original developer's tested behavior verbatim. Did not challenge whether the user-visible promise (MCM hint text) matched the actual implementation. | Added a feature-port checklist item: when porting a feature with multiple modes, **read the user-facing strings (MCM hints, dropdown labels) and trace them to the implementation**. If the promise doesn't match the code, either fix the code or fix the promise — never ship the mismatch. |

## Next steps

- Build green (verified — 1405/1405 tests pass).
- Update CHANGELOG.md with the three Codex-driven fixes.
- Update [docs/features/siege-dismount.md](../features/siege-dismount.md) — remove the "ItemModifier loss" known limitation (now fixed); update the behavior modes table (mode 1 now reserved); add a "modded sieges must set IsSiegeBattle" note.
- Update [AGENTS.md](../../AGENTS.md) "Lessons From Prior Reviews" section.
- Add this review to [REVIEW-LOG.md](REVIEW-LOG.md).
