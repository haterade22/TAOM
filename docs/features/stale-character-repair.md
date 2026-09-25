# Stale Character Repair

**Status:** implemented 2026-09-06 · **Patch category:** `Patch83_StaleCharacterRepair` · **Code:** `Main/Features/StaleCharacterRepair/`, `Main/Adapters/StaleCharacterAdapter.cs` · **Crash bundle:** `065939b6` (2026-09-05, engine v1.4.8)

## Overview

Makes a character the save restored under an id that current ModuleData no longer defines **inert**,
so the engine's several unguarded dereferences of its null fields cannot fire. Without it, loading
such a save is a hard `NullReferenceException` inside `Campaign.OnGameLoaded`, which the player sees
as "A problem occured while trying to load the saved game." or as a crash report with a TAOM game
model on the stack.

No TAOM code causes the nulls. TAOM appears on the stack only because it owns the registered
`PartyMoraleModel` slot, and the failing line is inside the `base` call.

## The crash that produced it

Read from the decompiled v1.4.8 assemblies, each link verified rather than inferred:

| # | Where | What it does |
|---|---|---|
| 1 | `Campaign.OnGameLoaded:688` | calls `CampaignObjectManager.AfterLoad()`, which runs `Clan.AfterLoad` → `UpdateCurrentStrength` → `PartyBase.EstimatedStrength` → `DefaultMilitaryPowerModel.GetPowerOfParty` → `MobileParty.Morale` → the registered `PartyMoraleModel` |
| 2 | `DefaultPartyMoraleModel.GetMoraleEffectsFromSkill:206-213` | resolves a character through `SkillHelper.GetEffectivePartyLeaderForSkill` and **null-checks it**, so the character is not null |
| 3 | `SkillHelper.GetEffectivePartyLeaderForSkill:78-94` | for a party with **no leader hero** returns `party.MemberRoster.GetCharacterAtIndex(0)`: a plain troop, not a hero. Garrisons and militia are exactly the leaderless clan-owned parties step 1 walks |
| 4 | `CharacterObject.GetSkillValue:791-798` | routes a non-hero to `BasicCharacterObject.GetSkillValue:292-295`, which is `return DefaultCharacterSkills.Skills.GetPropertyValue(skill);` with no guard at all |

Step 4 is a one-line method, so a release JIT inlines it. That is why the crash report names the
`CharacterObject.GetSkillValue` frame rather than the base method that actually threw.

## Why the fields are null

Two rival explanations were eliminated by reading the source, which is what narrowed this to one
cause:

- **`MBCharacterSkills.Skills` is never null.** Its constructor assigns the `PropertyOwner`, and
  `PropertyOwner.GetPropertyValue` returns 0 for an unknown attribute rather than throwing.
- **`PropertyOwner._attributes` is not null after a save round-trip**, despite
  `ObjectLoadData.cs:133` using `FormatterServices.GetUninitializedObject` (no constructor). The
  field is `protected`, so `TypeDefinition.CollectFields` collects it as `[SaveableField(10)]` and
  restores it. The bundle proves it independently: the crash reporter printed `morale=62` for a
  party on the same load, which requires that exact dictionary lookup to have worked.

The actual mechanism, and it is provable rather than merely plausible:

1. Save objects are materialised and **self-register** first: `MBObjectBase`'s
   `[LoadInitializationCallback] BeforeLoad()` calls `TryRegisterObjectWithoutInitialization`.
2. ModuleData XML then **upgrades those already-registered instances in place**:
   `MBObjectManager.CreateObjectFromXmlNode` calls `GetPresumedObject(elementName, id)` and runs
   `Deserialize` on the existing object.
3. A character whose id is no longer in ModuleData is never reached by step 2.

> **Open question (2026-09-25, #670):** a code reading during the #669 review found that
> `Campaign.cs:1457` unregisters every not-ready object during `OnInitialize`, before this
> patch's `PreAfterLoad` sweep reads `GetObjectTypeList<BasicCharacterObject>()`, which returns
> registered objects only. If that ordering holds, a character deleted from ModuleData is gone
> from the list before Patch83 looks. Unverified in game; #670 tracks the `/investigate`.

The bundle's own timestamps show the two passes eight seconds apart (`[SaveLoad] ObjectsInitialized`
at 20:16:48; `spnpccharacters.xml` opened at 20:16:56). The save was written by TAOM v2.0.18 on
Bannerlord v1.4.7 and loaded by TAOM v2.0.27 on v1.4.8: nine versions of troop-XML churn.

## Why the repair covers four fields, not one

`CharacterObject` persists **exactly two** fields:

```csharp
CharacterObject.cs:20   [SaveableField(101)] private Hero _heroObject;
CharacterObject.cs:23   [SaveableField(103)] private CharacterObject _originCharacter;
```

Everything else on it, and every field on `BasicCharacterObject`, comes from XML `Deserialize`.
`CharacterObject.Init()` restores four more (occupation, traits, level, restriction flags). So the
skills NRE is not *the* bug, it is merely the first null the load path happens to reach.

| Field | State on a stub | Next unguarded dereference | Repaired? |
|---|---|---|---|
| `DefaultCharacterSkills` | null | `BasicCharacterObject.cs:292`, the shipped crash | yes, `new MBCharacterSkills()` |
| `UpgradeTargets` | **null** (auto-property *initializer* at `CharacterObject.cs:314`, skipped by `GetUninitializedObject`) | `PartyCharacterVM.cs:1113` reads `.Length`, which crashes the party screen | yes, empty array |
| `BodyPropertyRange` | null | `BasicCharacterObject.cs:221` reads `.HairTags` on agent spawn | yes, mirroring vanilla's own fallback at `:472-474` |
| `_basicName` | null | `Name` returns it; `ToString()` calls `Name.ToString()` | yes, a `TextObject` of the StringId |
| `_culture` | null | any `character.Culture.StringId` | **no, deliberately** |
| `Level` | 1 (from `Init()`) | wrong `Tier` → wrong `GetPower()` → wrong party strength | **no, unknowable** |

Repairing only the skills would have converted a deterministic, immediately-diagnosable load-time
crash into a load that appears to succeed and then crashes later in the party screen or a battle,
with a stack naming nothing to do with save staleness, by which point the player would very likely
have saved over the file that reproduced it. That trade is strictly worse, which is why the scope
widened after review.

**`_culture` is left null on purpose.** Inventing one is a silent gameplay lie; a null surfaces
through TAOM's own adapter chokepoints, which is the behaviour the adapter rules were written for.
**`Level` cannot be recovered:** the real value lived only in the deleted XML.

**So the character is made inert, not correct.** It is a ghost with no culture and tier 0. The real
fix is the data, which is why the ids are logged and shown in game.

## Why the seam is `MBObjectManager.PreAfterLoad`

Ordering forces a patch rather than a behavior. `Campaign.OnGameLoaded` (v1.4.8:679-695) runs:

```
:683   base.ObjectManager.PreAfterLoad();       <-- Patch83 postfixes this
:687   base.ObjectManager.AfterLoad();
:688   CampaignObjectManager.AfterLoad();       <-- the crash is in here
:691   CampaignEventDispatcher.OnGameEarlyLoaded(starter);
:692   CampaignEventDispatcher.OnGameLoaded(starter);
```

Both load events are dispatched **after** the crash, so a `CampaignBehaviorBase` subscribing to
`OnGameLoadedEvent` could never run in time, and the late `OnGameInitializationFinished` batch is
later still. The patch is therefore applied from `SubModule.OnSubModuleLoad`.

`PreAfterLoad` and `AfterLoad` both have exactly one call site in the whole engine and both run
before the crash. `PreAfterLoad` is strictly earlier and additionally covers
`CampaignObjectManager` / `IssueManager` / `QuestManager.PreAfterLoad` at `:684-686`, so it is the
better of the two. (The first cut targeted `AfterLoad` and justified it with "by then vanilla has
fixed what it can", and that reasoning was wrong: `CharacterObject` overrides neither hook, so nothing
repairs these fields in between.)

**It fires once, on a saved-game load only.** `Campaign.OnGameLoaded` is reached from
`DoLoadingForGameType` only inside the `GameLoadingType.SavedCampaign` branch (`:1663-1670`); the
new-game branch calls `OnNewGameCreated` instead. A whole-install member-reference scan across every
DLL in `bin/` and `Modules/*/bin/` finds no other caller. A new game therefore never runs the sweep,
which is correct: a fresh campaign has no save-restored stubs.

## Why a repair rather than a guard at each read

The read sites are per-agent per-hit combat paths (`SandboxAgentStatCalculateModel`) and per-frame
UI paths (`PartyCharacterVM`). Guarding them all would tax the hottest code in the game for a
load-time data defect, and every future consumer would need its own guard. Making the object
well-formed fixes every consumer at once and costs one sweep per save load.

The repair is idempotent and only ever touches objects in the broken state: each field is filled
only when it is null, and `TryMakeInert` re-checks before writing, so a character another mod
repaired in between is left alone. That keeps it clear of the "destructive load-path operation"
hazard in `.claude/rules/csharp-architecture.md`.

## Why `MBCharacterSkills` is created unregistered

Vanilla's own fallback uses `MBObjectManager.Instance.CreateObject<MBCharacterSkills>(StringId)`,
which registers. The repair deliberately does not, and this was checked rather than assumed:

- **Nothing saves, indexes or GUID-resolves the field.** `DefaultCharacterSkills` carries no
  `[SaveableField]`, and `BasicCharacterObject.AutoGeneratedInstanceCollectObjects` adds nothing, so
  the save graph never reaches an `MBCharacterSkills`. A whole-install scan finds exactly one
  consumer outside `TaleWorlds.Core`.
- **Registering would be slightly worse.** It consumes an `MBGUID`, mutates three shared
  collections, and exposes the object to `MBObjectManager.UnregisterNonReadyObjects` for zero
  benefit. Vanilla registers only because `Deserialize` needs `skill_template` cross-references to
  resolve by id; the repair needs nothing of the sort.

`MBBodyProperty` **is** registered, because vanilla registers it at `BasicCharacterObject.cs:472-474`
and that type genuinely is resolved by id elsewhere. Note the consequence, which is a known TAOM
trap: a default `BodyProperties` is age 0, so a repaired troop renders as a child if it ever spawns.
That is shipped vanilla behaviour for a face-less character and is preferable to a hard crash.

## What the player sees

Nothing on a healthy load. That is deliberate: the sweep runs on every save load, and a line saying
"repaired 0" on every launch trains the reader to skip the one that matters.

When it repairs something, an in-game message names the count and points at the log. **This is not
cosmetic.** The stale ids are re-serialised into every subsequent save (the object owns
`[SaveableField(101/103)]` plus `MBObjectBase`'s `[SaveableProperty]`s) while the repair is not, so a
player who never opens the log would quietly overwrite their last recoverable file. The message is
deferred to `OnSessionLaunched` because the repair runs before the HUD that renders messages exists.

The log line carries the ids:

```
[StaleCharacterRepair] made 3 stale character(s) inert. Each was restored from the save under an id
that current ModuleData no longer defines, so several of its fields were null and the engine
dereferences them unguarded (crash bundle 065939b6). This is a DATA problem and the save still
carries these ids: they are written back into every future save, while the repair is not.
Ids: taom_x, taom_y, taom_z
```

Ids are capped at 20 with the true count kept, so a save that lost a whole culture does not produce
an unreadable wall.

## The five outcomes, and why a bool was not enough

`TryMakeInert` returns a `StubRepairOutcome`, not a `bool`. The first cut used a bool and collapsed
five meanings into "failed", two of them badly:

| Outcome | Meaning | Reported as |
|---|---|---|
| `Repaired` | at least one null field filled | counted, ids logged, player notified |
| `AlreadyHealthy` | something filled the fields between scan and write | **silent**, because the character is safe and calling it a crash risk is the opposite of true |
| `BindingUnavailable` | a reflected engine member did not resolve | **once**, naming the binding rather than the player's save data |
| `NotResolved` | no usable id, or the id did not resolve | listed as still dangerous |
| `Failed` | the write threw | listed as still dangerous |

`BindingUnavailable` is the important one: an engine rename makes *every* character fail, and the
bool version would have listed the player's troop ids under "could not repair" while logging nothing
about the actual cause.

## Components

| File | Role |
|---|---|
| `Main/Features/StaleCharacterRepair/Hooks/Patch83_StaleCharacterRepair.cs` | Postfix on `MBObjectManager.PreAfterLoad`. Thin: delegate and swallow. Has `ResetForUnload` |
| `Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs` | Sweep orchestration, the five-outcome report, the id cap, the notice trigger |
| `Main/Features/StaleCharacterRepair/Domain/StubRepairOutcome.cs` | The five outcomes |
| `Main/Adapters/StaleCharacterAdapter.cs` | Engine boundary: the scan, the four field writes, the deferred notice |

Reading needs no reflection (`GetDefaultCharacterSkills()`, `BodyPropertyRange`, `Name` and
`UpgradeTargets` are all public getters). Writing does: three of the four have `protected`/`private`
setters. All four `MemberInfo`s are resolved once in a static initialiser, never per character.

The adapter keeps the objects the current sweep found in a private map, so the repair writes to the
object the scan actually saw rather than round-tripping through a `StringId`.
`MBObjectManager.GetObject<T>` resolves the *first* assignable type record's match, which is not
guaranteed to be the same instance. The map is cleared at the start of every scan, so it never
outlives one sweep.

## Tests

| Suite | Covers |
|---|---|
| `StaleCharacterRepairServiceTests` (19) | healthy-load silence, null adapter result, single and multiple repairs, ids in the report, all five outcomes including the two that used to be mis-reported, a throwing repair, a throwing scan, a throwing logger, a throwing notice, notice-only-when-repaired, and the id-cap and blank-id policies |
| `Patch83StaleCharacterRepairBindingTests` (11) | the target and its arity, all four reflected members and their types, `MBCharacterSkills`/`MBBodyProperty` construction, `RegisterPresumedObject`, the category string, and the presence of `ResetForUnload` |

The binding suite matters more than usual because **every** binding here fails quietly. A renamed
target means Harmony throws at category-apply time, which `SubModule` now catches and logs (it
previously would have aborted the remaining module init); a renamed field means `AccessTools`
returns null and every repair returns `BindingUnavailable`.

Behaviour cannot be proven in unit tests: constructing a save-restored `CharacterObject` with null
fields needs a live campaign. The service tests cover the decision logic against a substituted
adapter instead.

## Verification not yet done

The repair is proven against the engine bindings and its own logic, not against the crashing save.
Confirming it end to end needs the player's `saveauto2` (or any save reproducing bundle
`065939b6`): load it, and expect the load to complete, an in-game notice, and one
`[StaleCharacterRepair] made N stale character(s) inert` line naming the stale ids. Those ids are
then the data fix. Open the party screen with the affected garrison afterwards, since that is the
crash site the widened scope exists to prevent.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
