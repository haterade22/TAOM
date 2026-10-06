# Bannerlord save system — SaveableTypeDefiner + SyncData (Phase 6)

> **One process, traced from the decompile** (`TaleWorlds.SaveSystem`, v1.4.5): how a feature persists state across
> save/load — the two mechanisms (`CampaignBehaviorBase.SyncData` for behavior state, `SaveableTypeDefiner` +
> `[SaveableField]` for custom classes) and the **base+localId collision gotcha** that has crashed TAOM. Many TAOM
> features persist here (SpecialResources, CultureConversion, Messengers, careers). Part of the phased engine study.

## WHAT it is

Two complementary mechanisms:
1. **`SyncData`** — a `CampaignBehaviorBase` reads/writes its own fields to the save via an `IDataStore`. The
   simplest path; needs no type registration for primitives, strings, lists of primitives, and already-registered
   types.
2. **`SaveableTypeDefiner` + `[SaveableField]`/`[SaveableProperty]`** — register a *custom class/struct/enum* so the
   serializer can persist instances of it (when a behavior wants to save a custom store object, not just primitives).
3. (And `MBObjectBase` objects — Monster/Item/Character/etc. — are saved by **`MBGUID`** using the `RegisterType`
   typeId from [Phase 5](object-system-mbobjectmanager.md), a *separate* id space from `SaveableTypeDefiner`.)

## HOW it works

### `CampaignBehaviorBase.SyncData(IDataStore dataStore)`
A behavior overrides `SyncData` and calls `dataStore.SyncData("unique_key", ref _field)` for each field to persist.
`IDataStore` is bidirectional (`IsSaving` true on save / false on load), so the same code path serializes both ways.
Works for primitives, strings, `List<>`/`Dictionary<>` of primitives, and any type the serializer knows (registered
types + `MBObjectBase`). **This is the default TAOM persistence path** — most features just `SyncData` a few fields
and need no `SaveableTypeDefiner`.

### `SaveableTypeDefiner` (SaveSystem.cs:1046) — register custom saveable types
```
protected SaveableTypeDefiner(int saveBaseId) { _saveBaseId = saveBaseId; }      // base id (ctor)
protected void AddClassDefinition(Type type, int saveId, …)                       // SaveSystem.cs:1123
    => new TypeDefinition(type, _saveBaseId + saveId, …);                         // ← real id = base + localId  ⭐
protected void AddEnumDefinition(Type type, int saveId, …)                        // :1167
protected void AddBasicTypeDefinition / AddStructDefinition / ConstructGenericClassDefinition …
// override the Define* virtuals to declare your types:
protected internal virtual void DefineClassTypes()   // :1066 — AddClassDefinition calls go here
protected internal virtual void DefineEnumTypes()    // :1082
protected internal virtual void DefineStructTypes / DefineContainerDefinitions / …
```
A mod subclasses `SaveableTypeDefiner(<base id>)`, overrides `DefineClassTypes()` (etc.), and calls
`AddClassDefinition(typeof(MyStore), <localId>)`. The serializer then knows how to save `MyStore`. The class's
**fields are tagged `[SaveableField(n)]`** (SaveSystem.cs:134) / properties `[SaveableProperty(n)]`, where `n` is the
field id *within that class*.

### ⚠️ The base+localId collision gotcha (confirmed at the source)
The actual registered id is **`_saveBaseId + saveId`** (SaveSystem.cs:1114/1125/1131). So a definer with base `B`
using localIds `1..N` occupies ids `B+1 .. B+N`. **If two definers' `(base+localId)` ranges overlap, the ids collide
→ a hard crash at `Module.Initialize`** (duplicate save-id). TAOM's convention: **bases step by 100**, so each
definer owns a ~100-id window — and a definer's localIds must stay within its window (don't let `localId` reach the
next definer's base offset). See `feedback_saveable_typedefiner_localid_offset` for the exact convention and the
worked collision (CareerQuest base `726900701` + localId 1 = `726900702` = FormationPreset base `726900601` + localId
101 — adjacent bases only 100 apart, so a localId of 101 spills into the next window). **When adding a new
SaveableTypeDefiner: pick a fresh base ≥100 past the last one, and keep localIds well inside the window.**

## WHY it's shaped this way

`SyncData` keeps the common case trivial (a behavior just lists its fields). The `SaveableTypeDefiner` id space is
*global* across all mods + vanilla, so each definer must carve a non-overlapping range — the `base + localId` scheme
lets a mod claim a block (the base) and number within it (localId). The split from the `MBObjectManager` typeId
space (Phase 5) is because `MBObjectBase` objects are referenced by `MBGUID` (they live in the registry), whereas
`SaveableTypeDefiner` types are plain serialized objects.

## TAOM relevance + gotchas
- **Default path = `SyncData` primitives/strings.** TAOM features routinely avoid a `SaveableTypeDefiner` by storing
  **composite strings** (e.g. SpecialResources `heroId:resourceId`, CultureConversion composite-string store,
  PendingMessenger primitive-dict) and `SyncData`-ing those — simpler + no base-id management. Prefer this when the
  state is expressible as primitives.
- **A `SaveableTypeDefiner` is only needed for a custom class/struct/enum** you must serialize directly. Then: unique
  base (≥100 past the last), `[SaveableField(n)]` on each field, localIds inside the window. **Collision = crash at
  Module.Initialize**, not a soft failure.
- **The elephant needs no SaveDefiner** (it's battle-only state) — confirmed in the [ADOD_Beasts comparison](../adod-beasts-architecture-and-taom-port.md); ADOD_Beasts's SaveDefiner persists the *wolf* (`_acquiredWolfId`). A creature-*troop* with no campaign state needs nothing here.
- `OnGameLoaded`/`SyncData` mutations on heroes/settlements must follow the entity-state-matrix rule
  (`.claude/rules/csharp-architecture.md`) — load-path mutation is destructive; guard it.
- New saveable validation: TAOM has no engine-level guard against base-id collision — discipline is on the author +
  the memory. There's no validator for this (unlike moduledata refs).

## Performance: what a save costs [yotthani]

Measured by yotthani in game and on an offline save bench (MithrilForge `docs/engine/perf.md`, sections K2 and
`save-no-forced-gc`, v1.5.3, 2026-10-02 to 2026-10-04; v1.5.4: not re-checked). One save of 145,000 to 152,000
objects.
- **`SaveContext.Save` takes 2.6 to 2.9 s**, all of it a frozen frame. It allocates about 1.72 GB to write about
  106 MB, and under the default workstation GC 66 to 72 % of that time is collections; the work alone is about
  0.5 s.
- **Server GC brings a save to about 1.05 s** in game (8 heaps, at about 1.5 GB more RAM), but the GC mode is
  fixed at runtime start: it can be set only in the starting exe's `.exe.config` (for BLSE,
  `Bannerlord.BLSE.Launcher.exe.config`), never by a mod.
- **The hitch after a save is vanilla's forced collection:** `Game.OnSaveCompleted` calls
  `Common.MemoryCleanupGC()`, a blocking collection: 313 and 286 ms about a second after the save under server
  GC; under workstation GC, Gen-2 collections of 835 and 872 ms came 5 to 6 s after an instrumented save, likely
  the same call. Removing it is worse: the runtime collects the same garbage later, longer (535 ms) and with
  about 2 GB more RAM in between. Vanilla's other forced collections on game state changes are in
  [battle-load-diagnostics.md](../../features/battle-load-diagnostics.md) (`OnPushState`, `OnPopState`).
- **Save bytes are not deterministic:** string ids depend on the order the parallel workers reach
  `AddOrGetStringId`, so two saves of one state differ. Compare a save-path patch in sequential mode or after a
  load, never byte for byte.
- **A save loaded offline without its load callbacks gives every `TextObject` a hash of 0** (`GetHashCode`
  returns an id only the callback assigns), so the save's object dictionary degenerates (`CollectObjects` 3.5 s
  instead of 0.2 s).

## The native boundary
The save *serializer* is largely managed (`TaleWorlds.SaveSystem` — `IDataStore`, `TypeDefinition`,
`DefinitionContext`); it walks tagged fields + registered types and writes the save blob. The objects it serializes
may reference native resources (an `MBObjectBase`'s `MBGUID`), but the persistence machinery is managed C#.

## Evidence (file:line, v1.4.5)
- `TaleWorlds.SaveSystem.cs`:1046 (`SaveableTypeDefiner`), 1052 (ctor `_saveBaseId`), 1123-1126 (`AddClassDefinition` → `_saveBaseId + saveId`), 1129-1135 (`AddClassDefinitionWithCustomFields`), 1167 (`AddEnumDefinition`), 1112 (`AddBasicTypeDefinition`), 1062-1100 (`Define*` virtuals), 134 (`SaveableFieldAttribute`).
- TAOM precedent + the collision rule: memory `feedback_saveable_typedefiner_localid_offset`; ADOD_Beasts's `ADODBeastsSaveDefiner` (the wolf) in the [ADOD_Beasts comparison](../adod-beasts-architecture-and-taom-port.md).
- `CampaignBehaviorBase.SyncData(IDataStore)` — the per-behavior persistence override (TAOM features override it widely).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)
- [docs/modding/id-cheatsheet.md](../../modding/id-cheatsheet.md)
- [docs/reference/engine/issue-and-quest-system.md](./issue-and-quest-system.md)

<!-- backlinks-end -->
