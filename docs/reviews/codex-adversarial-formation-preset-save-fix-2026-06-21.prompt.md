# Codex Adversarial Review — Formation Preset save-corruption fix (2026-06-21)

You are an independent adversarial reviewer. Assume the fix below is subtly wrong and try to prove it. Verify
every TaleWorlds API claim against the INSTALLED v1.4.x DLLs (use `ilspycmd` on
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\*.dll`; the decompile dump at
`E:\Decompiled_Bannerlord\` is a browsing aid, NOT authoritative for signatures). Read the actual TAOM source — do
not trust this summary.

## Background — the crash being fixed

A player CTD'd on EVERY campaign save once they saved an Order-of-Battle formation preset. Crash bundle
`taom_crash_20260621_200427_8754f009` (occurrence #2987). Exception chain:

```
System.AggregateException  @ Task`1.GetResultCore <- SaveOutput.PrintStatus <- Game.OnSaveCompleted <- Game.OnTick
  inner System.NullReferenceException
    @ TaleWorlds.SaveSystem.GameData.<>c.<Write>b__23_0(Byte[] x)   // x.Length on a null entry
    <- Enumerable.Sum <- GameData.Write <- FileDriver.Save <- AsyncFileSaveDriver...Save   // background thread
```

Diagnosed root cause: `HoNFormationPreset` had `[SaveableField(3)] private DateTime _createdAt`. `System.DateTime`
is not in TaleWorlds' serializable basic-type set (`SaveableBasicTypeDefiner`), and is not registered as a
class/struct/container in `FormationPresetSaveableTypeDefiner`. On save the engine fell through to the `CustomStruct`
path and a null serialized buffer landed in `GameData.ObjectData`/`ContainerData`, NRE'ing in `.Sum(x => x.Length)`
on the async save thread.

## The fix under review (read these files in the TAOM repo)

1. `Main/Features/CompanionTactics/FormationPresets/Models/HoNFormationPreset.cs`
   - Removed `[SaveableField(3)] DateTime _createdAt`, the `CreatedAt` property, and the `_createdAt = DateTime.Now`
     initializer. Field ids 1,2,4,5,6 kept unchanged; id 3 retired (gap intentional).
2. `Main/Features/CompanionTactics/FormationPresets/Models/FormationPresetSaveableTypeDefiner.cs` (unchanged — review for completeness)
   - BaseId 726900601, class 101; container defs `List<HoNFormationPreset>`, `Dictionary<string,int>`,
     `Dictionary<int,int>`, `List<string>`.
3. `Main/Features/CompanionTactics/FormationPresets/Hooks/FormationPresetCampaignBehavior.cs`
   - `SyncData` try/catch + comment correction. (The comment now states the catch guards LOAD/ref only, not the
     async save write.)
4. `Main/Features/TaomSettings.cs` + `Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs`
   - `EnableFormationPresets` default flipped `true → false` (+ provider `?? false` fallback).
5. `TAOM.Tests/Features/CompanionTactics/FormationPresets/HoNFormationPresetSerializationTests.cs` (NEW)
   - Reflection test asserting every `[SaveableField]` on `HoNFormationPreset` is serializable; ids unique; id 3 retired.

## Adversarial questions — try to find a real defect for each

1. **Did removing `[SaveableField(3)]` actually eliminate the crash, or just move it?** Decompile
   `TaleWorlds.SaveSystem.GameData.Write`, `SaveContext.WriteObjects`/`SaveSingleObject`, `VariableSaveData.SaveTo`,
   and `SaveableBasicTypeDefiner` on the INSTALLED DLLs. Confirm: (a) the remaining field types (string,
   `Dictionary<string,int>`, `List<string>`, `Dictionary<int,int>`) are ALL serializable, and (b) there is no OTHER
   unserializable member reachable from the persisted `List<HoNFormationPreset>` graph.

2. **Save-version / field-id compatibility.** Does retiring id 3 (leaving a gap) break loading a save that was written
   with id 3 present? Does TaleWorlds' field-id deserialization tolerate a removed field id, or does it require
   contiguous ids / fail on an unknown saved id? (Note: TAOM never successfully saved a preset before — but the
   external donor mod with BaseId 726900601 may have. Assess the real risk.)

3. **Is the `DateTime` truly the only cause of THIS crash?** The dump names only "a null buffer," not the type. Could
   the null entry come from a different object in the save graph (the player runs third-party mods
   `TAOMTweaks.*` + `TAOMCultureAlignmentOverhaul`)? Argue whether the TAOM fix is necessary AND/OR sufficient.

4. **Does the default-flip to OFF leave any path that still serializes a preset?** Trace: with
   `EnableFormationPresets=false`, can a `HoNFormationPreset` still enter the persisted list (service singleton
   surviving across sessions, OOBOverlayService gating, FormationPresetCampaignBehavior registered unconditionally)?
   Is there a scenario where an existing in-memory preset still gets serialized after the toggle is turned off?

5. **Regression test correctness.** Read `HoNFormationPresetSerializationTests`. Does its "serializable" predicate
   match the engine's ACTUAL rule? Specifically: does the engine serialize `enum` `[SaveableField]`s (the test
   currently rejects them)? Does it accept the Library structs the test allowlists by name? Are there false
   positives (test passes a type the engine rejects) or false negatives (test fails a type the engine accepts)?
   Would the test actually fail if `DateTime` were re-added? (It was verified to, but confirm the logic.)

6. **The unconditional `SaveableTypeDefiner` + behavior registration.** Is there any downside to leaving
   `FormationPresetSaveableTypeDefiner` (BaseId 726900601) auto-registered when the feature is off — e.g. BaseId
   collision risk, or wasted save bytes? Is the documented "matches the original developer mod for save-import
   compat" rationale still meaningful after the field layout changed?

7. **Anything else** — TDD/ADR-002/ADR-007 compliance on the touched files, thread-safety of the service singleton
   across missions, or any other latent defect.

For each finding: severity (HIGH/MED/LOW), file:line, the evidence (decompiled signature or source quote), and the
minimal fix. If the fix is correct and complete, say so explicitly per question — don't manufacture findings.
