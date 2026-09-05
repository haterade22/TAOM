Adversarial review of a small fix in TAOM, a Mount & Blade II: Bannerlord 1.4.8 total-conversion mod. Find real bugs; assume the author was confident and therefore careless somewhere.

## What was broken, and how it was diagnosed

The Player Switcher (#514) shows a panel at character creation listing lords of your culture. Clicking one previews that lord on the live 3D model.

In game, for the Isengard culture: clicking an `uruk_hai` lord changed the face but left the model the previous race. Clicking the one `uruk` lord (Sharku) worked. A diagnostic build produced `taom_debug_2026-08-28_12-12-18.log`, which showed every click reaching the ViewModel, `applied=True` every time, and `targetRace=5 bodyGenRace=2 -> bodyGenRaceAfter=2` on the failing transition. The log also carried `ArgumentOutOfRangeException` warnings for exactly the uruk_hai lords and never for Sharku.

Root cause, traced in `E:\Decompiled_Bannerlord\_shipping_build_v1.4.8\TaleWorlds.MountAndBlade.ViewModelCollection.cs` and `E:\Decompiled_Bannerlord\_categories_v1.4.8`:

- `FaceGenVM.SetBodyProperties(BodyProperties, bool ignoreDebugValues, int race, int gender, bool recordChange)` assigns `_faceGenerationParams.CurrentRace = race` near the top (guarded by `_isRaceAvailable`), computes `flag = CurrentRace != race`, and then calls `Refresh(clearProperties: true)` when `flag`, otherwise `UpdateFacegen(); UpdateFace();`.
- `Refresh` calls `UpdateVoiceIndiciesFromCurrentParameters()` partway through, which calls `GetVoiceUIIndex()`, which loops `for (i = 0; i < _faceGenerationParams.CurrentVoice; i++)` indexing `_isVoiceTypeUsableForOnlyNpc`. That list is sized for the TARGET race; `CurrentVoice` is decoded from the LORD's body-properties key. A lord encoding a voice index the target race lacks runs off the end and throws.
- The throw aborts `Refresh` before `UpdateFace()`, and `UpdateFace` is what calls `BodyGenerator.RefreshFace`, the only assignment of `BodyGenerator.Race` outside the constructor. So the race never commits.

## The fix under review

In `Main/Features/PlayerSwitcher/Hooks/BodyGeneratorPreviewSink.cs`, `ApplyPreview` now catches `ArgumentOutOfRangeException` and calls `vm.SetBodyProperties(...)` a second time with identical arguments.

The stated reasoning: `CurrentRace` was already assigned before the throw, so on the retry `flag` is false, the call takes the `UpdateFacegen(); UpdateFace();` branch, and that commits race and face through `RefreshFace` without touching the voice list.

## Attack these

1. **Does the retry actually work?** The decisive question is `_characterRefreshEnabled`. `SetBodyProperties` sets it false on entry and true immediately before `Refresh`; `Refresh` sets it false at its start, then throws. Trace its exact value entering the retry and confirm whether `UpdateFace()`'s `if (_characterRefreshEnabled)` guard passes. If it does not, `RefreshFace` never runs and the fix is a no-op that merely stops logging a warning. Verify from the decompiled source, not from the comment in the code.
2. **`_isRaceAvailable == false`.** Then `CurrentRace` is never assigned and `flag` stays false. What does the first call do, can it still throw, and what does the retry do?
3. **Second-order damage.** After the retry, the `SoundPreset` FaceGenPropertyVM is not rebuilt for the new race. Trace whether anything later reads it: `GetVoiceRealIndex`, `UpdateFace(int keyNo, ...)`, the voice slider, and especially the Done and GoToIndex paths which call `BodyGen.SaveCurrentCharacter()` and persist body/race/gender into `CharacterObject.PlayerCharacter`. Can a stale voice index be written into the player's saved character?
4. **`applied` correctness.** `applied = true` is set after `Dress(hero)`. The `finally` uses it to decide whether to clear `IsPreviewActive`, which gates TAOM's `Patch9_RaceFilter`. Can `applied` be true while the race did not commit, or false while it did?
5. **The player's own character.** `RestoreDefault` restores a snapshot and then calls `SaveCurrentCharacter()`. Confirm it cannot persist a LORD's appearance onto the player's created character, and that `TakeSnapshotOnce` cannot capture an already-mutated state on a retry or a failed first preview. This is the irreversible failure class.
6. **Diagnostics cost.** `PlayerSwitcherVM.OnRowClicked` and `ApplyPreview` log at INFO on every click. Read `Main/Core/Logging/FileLogger.cs` before costing this: TAOM drains INFO synchronously with a flush on the calling thread, deliberately, so a crash preserves the tail. State the real cost rather than assuming a syscall price, and do not recommend a level downgrade without reading that file.
7. Anything else: null paths on computed TaleWorlds getters, exception paths that leave the VM half-updated, re-entrancy across rapid repeated clicks.

## Output

Per finding: severity (P1 blocking / P2 should fix / P3 nice to have), file and line, what is wrong, why it matters in a real campaign, and the minimal fix. Separate CONFIRMED (verified against engine source) from SUSPECTED. If question 1 checks out, say so explicitly with the evidence: a clean bill there is the main point of this review. Do not invent findings to fill the report.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
