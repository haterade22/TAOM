# Character Skills Repair

**Status:** implemented 2026-09-06 · **Patch category:** `Patch83_CharacterSkillsRepair` · **Code:** `Main/Features/CharacterSkillsRepair/`, `Main/Adapters/CharacterSkillsAdapter.cs` · **Crash bundle:** `065939b6` (2026-09-05, engine v1.4.8)

## Overview

Gives an empty skill set to any character restored from a save whose XML definition no longer
exists, so vanilla's unguarded `BasicCharacterObject.GetSkillValue` cannot dereference a null on it.
Without the repair, loading such a save is a hard `NullReferenceException` inside
`Campaign.OnGameLoaded`, which the player sees as "A problem occured while trying to load the saved
game." or as a crash report with a TAOM game model on the stack.

No TAOM code causes the null. TAOM appears on the stack only because it owns the registered
`PartyMoraleModel` slot, and the failing line is inside the `base` call.

## The failure chain

Read from the decompiled v1.4.8 assemblies, each link verified rather than inferred:

| # | Where | What it does |
|---|---|---|
| 1 | `Campaign.OnGameLoaded:688` | calls `CampaignObjectManager.AfterLoad()`, which runs `Clan.AfterLoad` → `UpdateCurrentStrength` → `PartyBase.EstimatedStrength` → `DefaultMilitaryPowerModel.GetPowerOfParty` → `MobileParty.Morale` → the registered `PartyMoraleModel` |
| 2 | `DefaultPartyMoraleModel.GetMoraleEffectsFromSkill:206-213` | resolves a character through `SkillHelper.GetEffectivePartyLeaderForSkill` and **null-checks it**, so the character is not null |
| 3 | `SkillHelper.GetEffectivePartyLeaderForSkill:78-94` | for a party with **no leader hero** returns `party.MemberRoster.GetCharacterAtIndex(0)`: a plain troop, not a hero. Garrisons and militia are exactly the leaderless clan-owned parties step 1 walks |
| 4 | `CharacterObject.GetSkillValue:791-798` | routes a non-hero to `BasicCharacterObject.GetSkillValue:292-295`, which is `return DefaultCharacterSkills.Skills.GetPropertyValue(skill);` with no guard at all |

Step 4 is a one-line method, so a release JIT inlines it. That is why the crash report names the
`CharacterObject.GetSkillValue` frame rather than the base method that actually threw.

## Why the field can be null

Two candidates were ruled out by reading the source, which is what narrowed this to one cause:

- **`MBCharacterSkills.Skills` is never null.** Its constructor assigns the `PropertyOwner`
  (`MBCharacterSkills.cs:10-13`), and `PropertyOwner.GetPropertyValue` returns 0 for an unknown
  attribute rather than throwing. So the inner `.Skills` is not the null.
- **A troop from module XML always has a skill set.** `BasicCharacterObject.Deserialize:337-345`
  assigns `DefaultCharacterSkills` either from the referenced `skill_template` or from a fresh
  `CreateObject<MBCharacterSkills>`, so even a troop declaring neither is safe.

That leaves a character that **never went through XML deserialization**. `CharacterObject`'s
`[LoadInitializationCallback]` runs `Init()` (`CharacterObject.cs:402-414`), which sets occupation,
traits, level and restriction flags and nothing else. The three vanilla paths that would fill the
field in (`Deserialize`, `FillFrom`, `InitializeHeroBasicCharacterOnAfterLoad`) are all unreachable
for an object the save restored under an id that current ModuleData no longer defines.

**So this is a data symptom, not just an engine hazard.** Renaming or removing a troop between mod
versions is enough to produce one. The repair keeps the save loadable; the warning it logs names the
ids so the underlying data problem stays findable.

## Why the seam is `MBObjectManager.AfterLoad`

Ordering forces it, and the ordering is worth stating plainly because it rules out the obvious
design. `Campaign.OnGameLoaded` (v1.4.8:679-695) runs:

```
:687   base.ObjectManager.AfterLoad();          <-- Patch83 postfixes this
:688   CampaignObjectManager.AfterLoad();       <-- the crash is in here
:691   CampaignEventDispatcher.OnGameEarlyLoaded(starter);
:692   CampaignEventDispatcher.OnGameLoaded(starter);
```

Both load events are dispatched **after** the crash. A `CampaignBehaviorBase` subscribing to
`OnGameLoadedEvent`, the natural home for a load-time repair, could never run in time. The
postfix on the public, parameterless `MBObjectManager.AfterLoad` is the last point before the
crashing call, and by then every object's `AfterLoadInternal` has run, so anything vanilla could
still fix for itself has been fixed.

The patch is applied from `SubModule.OnSubModuleLoad`, not the late `OnGameInitializationFinished`
batch, for the same reason `Patch58_SkipCampaignIntro` is: the target fires during the load sequence,
so the patch must already be attached before any save can be loaded.

The postfix also fires on a new game and on the initial data load. The sweep finds nothing there and
returns silently, which is cheaper than trying to tell the cases apart.

## Why a repair rather than a guard at the read

- **The read site is a hot path.** `GetSkillValue` is called per agent per hit in combat. A Harmony
  prefix there would tax the hottest path in the game to fix a load-time data defect.
- **The morale model is only the path that crashed first.** `SkillHelper.AddSkillBonusForCharacter`
  and `AddSkillBonusForTown` (`SkillHelper.cs:22-47`) reach the same unguarded line for characters
  resolved the same way, and TAOM overrides several models that call into them. A guard in
  `TaomPartyMoraleModel` alone would leave the rest exposed.

Making the character well-formed fixes every consumer at once and costs one sweep per load.

The repair is idempotent and only ever touches objects in the broken state: a healthy character
fails the null test and is skipped, and `TryGiveEmptySkillSet` re-checks the field before writing so
a character another mod repaired in between is left alone. That is what keeps it clear of the
"destructive load-path operation" hazard in `.claude/rules/csharp-architecture.md`.

## Components

| File | Role |
|---|---|
| `Main/Features/CharacterSkillsRepair/Hooks/Patch83_CharacterSkillsRepair.cs` | Postfix on `MBObjectManager.AfterLoad`. Thin: resolve, delegate, swallow |
| `Main/Features/CharacterSkillsRepair/CharacterSkillsRepairService.cs` | Sweep orchestration, the report, the id cap |
| `Main/Adapters/CharacterSkillsAdapter.cs` | Engine boundary. Reads via the public `GetDefaultCharacterSkills()`; writes the protected `DefaultCharacterSkills` field through a cached `AccessTools.Field` |
| `Main/Features/CharacterSkillsRepair/CharacterSkillsRepairIoC.cs` | `Reuse.Singleton` registrations |

Reading needs no reflection (`GetDefaultCharacterSkills()` is public, `BasicCharacterObject.cs:287`).
Writing does: the field is `protected` with no setter. The `FieldInfo` is resolved once in a static
initialiser, never per character.

## What it logs

Nothing on a healthy load. That is deliberate: the sweep runs on every load, and a line saying
"repaired 0" on every launch trains the reader to skip the one that matters.

When it finds something:

```
[CharacterSkillsRepair] gave an empty skill set to 3 character(s) that had none. Vanilla
BasicCharacterObject.GetSkillValue derefs that field unguarded, so any skill read on one of these
was a hard NRE (crash bundle 065939b6). This is a DATA problem: each id below is defined in the
save but not in current ModuleData. Ids: taom_x, taom_y, taom_z
```

Ids are capped at 20 with the true count kept (`CharacterSkillsRepairService.MaxNamedIds`), so a
save that lost a whole culture's troops does not produce an unreadable wall. A character the repair
could **not** fix gets its own warning, because a skill read on one of those can still crash the
campaign.

## Tests

| Suite | Covers |
|---|---|
| `CharacterSkillsRepairServiceTests` (12) | healthy-load silence, null adapter result, single and multiple repairs, the id in the report, partial failure, a throwing repair, a throwing scan, a throwing logger, and the id-cap policy |
| `Patch83CharacterSkillsRepairBindingTests` (5) | `MBObjectManager.AfterLoad` resolves and is still parameterless; `BasicCharacterObject.DefaultCharacterSkills` resolves and is still an `MBCharacterSkills`; `GetDefaultCharacterSkills` is still public; `MBCharacterSkills` still has a parameterless ctor and a `Skills` property; the category string matches `SubModule` |

The binding suite matters more than usual here because **both** engine bindings fail quietly. A
renamed target means Harmony throws at category-apply time, `SubModule`'s guarded loop logs it and
carries on, and the repair never runs. A renamed field means `AccessTools.Field` returns null and
every repair returns false. Either drift turns a shipped crash fix back into the crash it fixed.

Behaviour cannot be proven in unit tests: constructing a save-restored `CharacterObject` with a null
skill set needs a live campaign. The service tests cover the decision logic against a substituted
adapter instead.

## Verification not yet done

The repair is proven against the engine bindings and its own logic, not against the crashing save.
Confirming it end to end needs the player's `saveauto2` (or any save that reproduces bundle
`065939b6`): load it, and expect the load to complete plus one
`[CharacterSkillsRepair] gave an empty skill set to N character(s)` line naming the stale troop ids.
Those ids are then the data fix.
