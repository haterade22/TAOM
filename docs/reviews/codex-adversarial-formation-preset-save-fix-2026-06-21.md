# Codex Adversarial Review — Formation Preset save-corruption fix (2026-06-21)

> Trimmed to the actionable final report. The raw `codex exec` run also emitted ~1.7 MB of decompiled-source
> reasoning trace (verifying every claim against the installed v1.4.x DLLs via `ilspycmd`); that trace was discarded
> as it contained terminal control characters and is not a durable artifact. Prompt:
> `codex-adversarial-formation-preset-save-fix-2026-06-21.prompt.md`. Findings verified + actioned in
> `rca-formation-preset-save-fix-2026-06-21.md`.

---

No CRITICAL/HIGH/MEDIUM findings. The `DateTime` removal is the right fix for the TAOM formation-preset save graph.

**LOW**

[LOW] `TAOM.Tests/Features/CompanionTactics/FormationPresets/HoNFormationPresetSerializationTests.cs:110` —
Regression test correctness — The test accepts any `List<>` / `Dictionary<,>` whose element types look serializable,
but the engine requires an exact registered container definition. Installed
`DefinitionContext.GetContainerDefinition(Type)` is an exact dictionary lookup, and `ConstructContainerDefinition(Type)`
registers concrete closed types only. The test would pass a future `Dictionary<float,int>` field even though the
engine has no registered container for it. It also false-rejects registered enums: installed `VariableSaveData` has
an `EnumDefinition` branch, and `SaveableCoreTypeDefiner` registers enums like `FormationClass`. Current fields are
still covered, and `DateTime` would fail. — Fix: make the test use exact registered container/enum allowlists, or
reflect a real `DefinitionContext`.

[LOW] `Main/Features/CompanionTactics/FormationPresets/Models/FormationPresetSaveableTypeDefiner.cs:28` —
SaveableTypeDefiner hygiene — `Dictionary<string,int>`, `Dictionary<int,int>`, and `List<string>` are already
registered by installed `SaveableBasicTypeDefiner.DefineContainerDefinitions()`. Installed
`SaveableTypeDefiner.ConstructContainerDefinition(Type)` calls `Debug.FailedAssert("duplicate definition...")` when a
type already exists. This is not the save-corruption crash and does not add save bytes, but the definer is
auto-discovered even when the feature is off, so it can emit duplicate-definition asserts during save-system
initialization. — Fix: only register `List<HoNFormationPreset>` here; rely on vanilla basic container definitions for
the primitive/string member containers.

**Question Results**

1. Clean. Remaining `HoNFormationPreset` fields are `string`, `Dictionary<string,int>`, `List<string>`,
   `Dictionary<int,int>`, all registered in installed DLLs; no other `[SaveableField]` members remain.
2. Clean for TAOM saves. Installed `FieldLoadData.FillObject()` looks up saved ids and skips unknown ids, so id 3
   does not need contiguity. Donor-mod import is only best-effort if their old id 3 required a type this build does
   not register.
3. Necessary for this TAOM crash; sufficient for the TAOM formation-preset graph. A different third-party mod could
   still produce a separate null buffer.
4. Clean. OFF blocks new UI-created presets. Existing loaded/in-memory presets still serialize by design through
   unconditional `SyncData`, now safely.
5. Low test precision issue above; current regression still catches `DateTime`.
6. No wasted bytes from feature-off behavior. BaseId collision risk exists only if another loaded mod registers the
   same ids; keeping the id is meaningful for import compatibility when TAOM replaces, rather than co-runs with, the
   donor mod.
7. No ADR-002/ADR-007/thread-safety blocker found in the touched files.

Verification note: Codex used `ilspycmd` against the installed Steam DLLs. It could not run the MSTest filter
(MSBuild SDK probing was denied access to `C:\Users\mikew\AppData\Local\Microsoft SDKs` in its sandbox); the test
run was performed separately by Claude (84/84 CompanionTactics tests pass).

CRITICAL: 0 | HIGH: 0 | MEDIUM: 0 | LOW: 2
VERDICT: ISSUES FOUND (both LOW, both fixed — see RCA)
