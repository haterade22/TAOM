# RCA — v2.0.9 "A problem occured while trying to load the saved game." (momentum SyncData > 32 KB)

**Date:** 2026-07-07
**Severity:** CRITICAL — silent save corruption at write time; every save past ~day 50 of a developed campaign became permanently unloadable.
**Feature:** `WarOfTheRingMomentum` (#327, shipped in the 2026-07-03 v2.0.9 package).
**Reported by:** multiple players on Discord ("2nd campaign I'm losing as rohan due to this random error. Didn't have it in last patch"). One player independently root-caused it and supplied a forensic report + two failing saves (`Elves209.sav` day 52, `Vidugavia.sav` day 20).

## Symptom

Loading a recent save shows only the generic engine dialog **"A problem occured while trying to load the saved game."** No crash, no crash dump, no TAOM crash bundle. Older saves of the same campaign load fine; from one autosave to the next, every subsequent save is broken. The engine's own `LoadResult` error message is the hardcoded placeholder `"Not implemented"`.

## Root cause (two halves, both verified against the decompiled 1.4.6 engine + our code)

**1. TAOM half.** `WarOfTheRingMomentumBehavior.SyncData` serialized the entire momentum state — up to `MaxEventsPerType (100)` events × 6 `MomentumActionType`s × 2 sides, **each event carrying its full localized `Description`** — as ONE JSON string synced under `_taom_wotr_momentum_v2` ([WarOfTheRingMomentumBehavior.cs:86](../../Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs), [MomentumStateStore.cs:78](../../Main/Features/WarOfTheRingMomentum/MomentumStateStore.cs)). In a developed campaign that string crosses ~32 KB around day ~50.

**2. Engine half (TaleWorlds bug).** `ArchiveSerializer.SerializeEntry` writes each save-archive entry's length as a signed-int16 truncation, then writes the data in full:

```csharp
// TaleWorlds.SaveSystem.ArchiveSerializer.SerializeEntry (v1.4.6, line 27)
_writer.WriteShort((short)entry.Data.Length);   // 32-bit length → int16, silently truncated
_writer.WriteBytes(entry.Data);                  // data written IN FULL
```

Any archive entry whose data exceeds 32,767 bytes gets a **wrong length on disk**. On load, `ArchiveDeserializer.LoadFrom` reads the short length (`ReadShort` → `ReadBytes`), so:
- **32,768–65,535 B** → `(short)` is negative → `ReadBytes(negative)` → `OverflowException` ("array dimensions exceeded").
- **> 65,535 B** → length written mod 65536 → reader consumes too few bytes → stream desync → a later bogus length throws `ArgumentException: Source array was not long enough`.

The corruption happens at **write time** and is invisible until the next load. `LoadContext.Load` catches the exception and returns `false` (printing only `ex.Message`), which is why nothing reaches TAOM's CrashReport.

**Arithmetic confirmation.** `Elves209.sav`: momentum entry 72,915 B true, stored as `72915 mod 65536 = 7379` — the exact number the reporting player measured, and reproduced independently by `tools/repair_sav_strings.py`. `Vidugavia.sav`: 47,502 B → negative int16 → OverflowException.

## The trap: the fix that caused it

The single-string transport was itself a **fix** for an earlier persistence bug — syncing the state as a `Dictionary<string,string>` container did not round-trip the engine's `IDataStore` at ~1,000 entries (momentum/stats reset on reload; play-test fix 2026-07-03). The single string solved the round-trip but **traded a data-loss bug for a save-corruption bug**: the same growth that broke the container as a dictionary made the string cross the int16 entry limit. Neither the deep-review nor the Codex pass on #327 modeled the engine's per-entry byte limit, because both were reasoning about round-trip fidelity, not archive-serialization byte constraints.

## Fix

**Permanent (`MomentumSyncChunker`, chunked split).** The behavior now splits the serialized JSON across a count key (`_taom_wotr_momentum_v3_count`) plus `_taom_wotr_momentum_v3_{i}` chunk keys, each capped at 10,000 UTF-16 chars (≤30,000 UTF-8 bytes worst case — a proven margin under the 32,763-byte entry-data limit). No single synced string can reach the engine limit regardless of how the log grows. **Zero gameplay change** — descriptions, the 100/type cap, and the momentum math are untouched (chunking was chosen over dropping descriptions precisely because the UI `BreakdownVM` renders `ev.Description` in the momentum tooltip). `_v2`→`_v3` rename → a one-time momentum reset on old saves (kingdoms re-enroll on the next daily tick; campaign untouched).

**Recovery (`tools/repair_sav_strings.py`).** Offline, stdlib. Decompresses the save, parses the Strings archive (recovering the truncated entry length via the sequential-entry-id anchor: `true = (stored & 0xFFFF) + k·65536`, validated by the next entry's header or the exact archive end), resets the oversized momentum entry to an empty string, re-frames GameData (Strings is the last section → O(n) splice) and recompresses to `<name>_fixed.sav`. **Zero campaign-data loss** — only the cosmetic war-meter history clears; the repaired save loads on the vanilla engine, no runtime patch. Verified on both user saves.

**Tests.** `MomentumSyncChunkerTests` — round-trip, empty/null, single-chunk, every-chunk-under-limit (incl. multibyte UTF-8), and the end-to-end proof that a realistic max log exceeds the limit as one string but every chunk stays safe and round-trips losslessly.

## Why missed / Prevent

- **Why missed:** the #327 reviews verified persistence *round-trip fidelity* (does the state survive save/load?) but never checked the *byte size* of any single `SyncData` string against the engine's archive-entry limit. The limit is undocumented and only bites at scale (day ~50+), so nothing in the test suite or the review's mental model surfaced it. The single-string transport was reviewed as a fix, not as a new risk.
- **Prevent:** LESSONS-LEARNED "State, Lifecycle & Save" now carries the rule **"A single SyncData string must stay under 32,767 UTF-8 bytes — chunk any unbounded/growing string payload."** The `SaveLoadDiagnostics` feature (Patch61) added the same week stamps `[SaveLoad] GraphFault kind=archiveParse` at exactly this failure site, so a recurrence self-identifies in a user's `taom_debug` log. `tools/inspect_sav.py` / `repair_sav_strings.py` give offline triage + recovery.
- **Process follow-up:** the v2.0.9 package spanned **34 unversioned commits** — two users both reading "v2.0.9" ran materially different persistence code, which badly slowed field triage. Bump the module version on every distributed package (and the `TAOM_Build` metadata stamp from Patch61 now self-identifies each save's exact build going forward).

## Artifacts

- Fix: `Main/Features/WarOfTheRingMomentum/MomentumSyncChunker.cs` + `WarOfTheRingMomentumBehavior.cs`
- Tools: `tools/repair_sav_strings.py`, `tools/inspect_sav.py`
- Tests: `TAOM.Tests/Features/WarOfTheRingMomentum/MomentumSyncChunkerTests.cs`
- Diagnostics: `Main/Features/SaveLoadDiagnostics/` (Patch61) — see `docs/features/save-load-diagnostics.md`
- Engine evidence: `ArchiveSerializer.cs:27`, `ArchiveDeserializer.LoadFrom`, `BinaryReader.ReadShort/ReadBytes` (installed 1.4.6)
