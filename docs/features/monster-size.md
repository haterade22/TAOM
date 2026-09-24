# Monster Size

## Overview

A mount's size is authored on its **Monster**, as the TAOM attribute `taom_body_length`, instead of on each Horse
item. TAOM copies it into the `body_length` of every Horse item that names the Monster at game init, so the engine
builds the mount at that size, and the antler reach of the creatures that opt in follows the live size. A resize is
one edit in the Monster file: no C# constant, no rebuild. In use on the great elk (110), the Animalia elk (100) and
the Animalia moose (150).

## Why This Exists

- **Vanilla behaviour:** the engine has no size on `<Monster>`. `Monster.Deserialize` (v1.5.3) reads no scale or size
  attribute. A mount is sized only from the ridden item: `Mission.BuildAgent` calls
  `agent.SetInitialAgentScale(0.01f * HorseComponent.BodyLength)` (`Mission.cs:4051-4057`), and that setter is
  `internal` (`Agent.cs:5146`).
- **TAOM requirement:** Mike, 2026-09-23, after resizing the great elk three times and the moose once in one day:
  "The monster xml should control the size of the animal." Each resize had meant editing the item's `body_length`, a
  C# `AuthoredScale` constant that the antler reach was derived from, the test pinning the two together, and a
  redeploy.
- **Without it:** every size lives twice (item and C#), and an Armory reinstall that reverts the item leaves the
  reach tuned for a size the animal no longer has.

## Architecture

### Design Challenge

Where TAOM can put a number the engine never reads, and how it reaches the three things that depend on size: the
agent's scale (set natively at build from `HorseComponent.BodyLength`), the rider's camera (which reads the mount's
`AgentScale`, `MissionScreen.cs:1984`), the tournament simulator (through the item's cached `Effectiveness`), and
TAOM's reach.

### Solution Approach

- **Write the item, not the agent.** `HorseComponent.BodyLength` has a private setter (`HorseComponent.cs:33`). Writing
  it once per game init, after the items load and before any mission, makes every engine reader agree with no patch:
  the agent scale at build, and through it the rider's camera; the one other direct reader is the spectator branch
  of `MissionScreen.UpdateCamera` (`MissionScreen.cs:1928`). The one
  value the engine caches from it, `ItemObject.Effectiveness` (computed at load, `ItemObject.cs:476`/`:671`, read only
  by the tournament simulator), is recomputed right after the write through the private `CalculateEffectiveness()`.
- **When.** `MBSubModuleBase.OnGameInitializationFinished` runs after the items exist in both game types: Custom
  Battle calls it after `LoadCustomGameXmls()` (`CustomGame.cs:49-56`), the campaign at the end of its init
  (`Campaign.cs:1471`), after `LoadXML("Items")` (`:1490`), for a new campaign and a loaded save alike. Each game
  rebuilds its items from XML, so the pass runs on every init, above `SubModule`'s once-per-process patch guard.
- **Where the number is read.** The Monster object keeps none of TAOM's attribute, so the adapter re-reads the merged
  Monsters XML with the engine's own game-type filter (`GetMergedXmlForManaged("Monsters", skipValidation: true,
  ignoreGameTypeInclusionCheck: false, gameType)`, as `Game.LoadBasicFiles` does), so a Custom Battle reads exactly
  the Monster files it loaded, every module's override included. The engine prints one `opening <path>` line per
  file for this re-read (about a dozen per game init).
- **The Monster wins, and the item keeps a placeholder.** The engine's `Items.xsd` makes `body_length` REQUIRED on
  `<Horse>` (dropping it fails TAOM's schema gate and would print a red "required but missing" line per item at
  load), so a sized Monster's items keep `body_length="100"` (`MonsterSizeConfig.ItemPlaceholderBodyLength`),
  and the pass overrides it; the summary line shows the old value. The data tests pin the item at exactly 100, so
  a size set on the item fails a test that says to resize the Monster, and a broken pass shows the mount at 1.0x
  rather than hiding behind a second copy of the size.
- **Reach follows the body.** `ElephantLikeCombatProfile` gained `reachScalesWithBody`: when set, the shared nodes
  multiply the 1.0x trigger range and radius by the creature's `Agent.AgentScale`, read once per scan and guarded by
  `ElephantLikeReach.Scale` (a value that is not finite, not positive, or over 10x reads as 1: NaN fails the gate;
  the gate is a plain positive requirement, so the engine's single-precision `0.01f * 10`, 0.099999994f, passes). The
  great elk and the Animalia elk and moose set it; the elephant and mumakil keep absolute metres, because their
  constants also size the howdah and the tower.

**Rejected:** a module schema. `GetMergedXmlForManaged` does look for `<module>/ModuleData/XmlSchemas/Monsters.xsd`
first, but `MBObjectManager.MergeElements` indexes `XmlResource.XsdElementDictionary` by the merged file's schema
path, and that dictionary is filled only from the game's own `XmlSchemas` folder (`XmlResource.cs:237-240`,
`:287-290`). A module schema would throw `KeyNotFoundException` while the engine merges the Armory's Monster file
after Native's. **Accepted cost:** the engine validates the Armory's Monster files against its own `Monsters.xsd`,
which does not declare `taom_body_length`, so it prints one red "The 'taom_body_length' attribute is not declared"
line per sized Monster at load. `MBObjectManager.ValidationEventHandler` only prints (`MBObjectManager.cs:1320-1337`)
and the file loads. TAOM's `tools/validate_xml_schemas.py` accepts the attribute through an explicit allowlist.

### Component Diagram

```
LOTRLOME_Armory Monsters/LOTR/*.xml   <Monster id="taom_animalia_moose" ... taom_body_length="150"/>
        | engine loads (one red "not declared" line per sized Monster)
SubModule.OnGameInitializationFinished (every game init)
        v
IMonsterSizeService.ApplyMonsterSizes      pure: whole numbers 10..1000, ordinal ids, the Monster wins
        | IMonsterSizeCatalogAdapter
        |   ReadDeclaredSizes   merged Monsters XML
        |   ReadHorseItems      every ItemObject with a HorseComponent
        |   SetBodyLength       HorseComponent.BodyLength private setter (AccessTools, cached)
        v
Mission.BuildAgent -> SetInitialAgentScale(0.01 x body_length)   engine: skeleton, clips, capsules, meshes
        v
ElephantLikeEngageDecorator / ElephantLikeAttackTaskBase
        reach x ElephantLikeReach.Scale(creature.AgentScale)   when the profile sets reachScalesWithBody
```

## Configuration

### Config File: `LOTRLOME_Armory/ModuleData/Monsters/LOTR/<file>.xml` (live, unversioned)

`taom_body_length="N"` on a `<Monster>`: a whole number from 10 to 1000 (`MonsterSizeConfig`), 100 being the size the
mesh was authored at. Anything else (a sign, a decimal point, an exponent, out of range) is refused with a
`[MonsterSize]` warning, and that Monster's mounts keep their items' own `body_length`. A size on a Monster that no
Horse item names (a typo'd or renamed id; ids are case-sensitive) is warned too, since the summary lists only the items
it changed. **A Monster shared by several items flattens them:** every item naming it takes the one size (the spiders
are 100 / 110 / 125 on `Monster.spider`, the wargs 110 / 110 / 115 on `Monster.warg`), so size a shared Monster only if
its items should match. **Reload scope:** read at every
game init, so a new Custom Battle or a loaded save picks up an edit; no rebuild. **Set the item's `body_length` to
the placeholder 100** when you size its Monster (the schema requires the attribute; the size lives on the Monster).

### Current Values

| Monster | File | `taom_body_length` | Withers in game |
|---|---|---|---|
| `taom_elk` (great elk, #636) | `lotr_monster_elk.xml` | 110 | 1.72 m |
| `taom_animalia_elk` (#646) | `lotr_monster_animalia.xml` | 100 | 1.68 m |
| `taom_animalia_moose` (#646) | `lotr_monster_animalia.xml` | 150 | 2.74 m |

Every other mount keeps its item's `body_length` (the war ram 100, the elephant 130, the mumakil 300). **Never size
the elephant or the mumakil here:** their howdah and tower prefabs are baked in metres for the item's size, so a
Monster size would move the beast under a platform that stays put. `HowdahPrefabTests` and `MumakilPlatformTests`
fail if either Monster declares `taom_body_length`. The war ram could be sized here; its reach would stay in metres
until its profile also sets `reachScalesWithBody`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MonsterSize/MonsterSizeConfig.cs` | The attribute name and its range |
| `Main/Features/MonsterSize/IMonsterSizeService.cs`, `MonsterSizeService.cs` | Parse, validate, plan and write; fail-soft |
| `Main/Features/MonsterSize/HorseItemRecord.cs` | What the service sees of an item |
| `Main/Adapters/IMonsterSizeCatalogAdapter.cs`, `MonsterSizeCatalogAdapter.cs` | Merged Monsters XML, the Horse items, the private setter |
| `Main/Features/MonsterSize/MonsterSizeIoC.cs`, `Main/IoC.cs`, `Main/SubModule.cs` (`OnGameInitializationFinished`) | Registration and the one call |
| `Main/Features/ElephantLike/ElephantLikeReach.cs` | The guarded scale |
| `Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeCombatProfile.cs` | `ReachScalesWithBody`, `ReachScaleOf` |
| `tools/validate_xml_schemas.py` | The TAOM extension-attribute allowlist |

## Dependencies

- **LOTRLOME_Armory** (unversioned): the Monster files that carry the attribute; ledgers
  [lotrlome-elk-changes.md](../reference/lotrlome-elk-changes.md),
  [lotrlome-animalia-changes.md](../reference/lotrlome-animalia-changes.md).
- **Engine:** `HorseComponent.BodyLength`'s private setter, the private `ItemObject.CalculateEffectiveness` and the
  `Effectiveness` setter (all three in `ReflectionSiteBindingTests`, so an engine update that renames one fails
  offline), and `MBObjectManager.GetMergedXmlForManaged`. At run time a missing setter logs an error per item and
  leaves the mount at its item's size, and a missing or failing recompute logs a warning with the size applied; the
  pass never throws.

## Tests

- `MonsterSizeServiceTests` (35): a sized Monster writes each of its items; already-at-size, unsized, monsterless,
  empty-id and differently-cased ids are left alone, and a size no item names (the case typo included) is warned; the
  placeholder is overridden with no warning, the summary
  showing the old value; 11 malformed or out-of-range values and a null are refused with a warning, plus one summary
  warning; 10, 1000 and a padded value are accepted; no declared size reads no items; a failed write logs an error
  and counts nothing; a failure part-way reports the items already written; a throwing catalog is logged, not
  thrown; `TryParseBodyLength`, the one parsing rule the data tests share.
- `MonsterSizeWiringTests` (3): the IoC registration, the container resolving the service, and the call sitting
  before the once-per-process guard. `ReflectionSiteBindingTests` pins the three engine members.
- `ElephantLikeReachTests` (14): every accepted size's engine scale (`0.01f * n` for 10 to 1000) passes through, as do
  0.05 and `float.Epsilon`; NaN, both infinities, 0, a negative and 11 read as 1.
- `ElkConfigTests.TheElkMonster_DeclaresItsSize_AndItsItemHoldsTheSchemaPlaceholder` and
  `AnimaliaMountWiringTests.AnimaliaMonsters_DeclareTheirSize_AndTheirItemsHoldTheSchemaPlaceholder`: the live Monsters declare a
  size `TryParseBodyLength` accepts and their items hold exactly the placeholder 100 (Inconclusive without the
  Armory). The reach tests pin the 1.0x values and the flag, and source pins (`ElkConfigTests.ElkProfile_PassesTheReachFlag`,
  `AnimaliaWiringTests.BothProfiles_PassTheReachFlag`) that each profile passes it. `HowdahPrefabTests` and
  `MumakilPlatformTests` pin that the elephant and mumakil Monsters carry no size.
- `tools/tests/test_apply_animalia_armory.py` `LiveRecipeParityTests`: the Animalia replay recipe matches the live
  Monsters and items attribute for attribute, so a live resize that skipped step 4 below fails here.
- The pass itself (the engine read and the private setter) is tested in game (ADR-008).

## How to resize a mount

1. Edit `taom_body_length` on its `<Monster>` in the live Armory (back the file up first).
2. Start a new Custom Battle (or load a save). The TAOM log shows
   `[MonsterSize] N Monster size(s), M Horse item(s) resized: <item>=<size>, ...`.
3. Look: the mount's size, the saddle on it, the rider's seat and camera, and the reach of its attack if it has one.
4. Record the new value in the creature's ledger, in its replay recipe (`MONSTER_FILE` in
   `tools/apply_animalia_armory.py` and its test for the Animalia animals; the elk ledger's section 1 for the great
   elk), and in the table above, or a reinstall's redo reverts it (`LiveRecipeParityTests` fails for the Animalia
   animals until the recipe says the same).

**In-game checks owed (2026-09-23, after a deploy: the installed 14:45 `TAOM.dll` has no size pass):** the TAOM log shows `[MonsterSize] 3 Monster size(s), 2 Horse item(s) resized: taom_elk_a=110 (was 100), taom_animalia_moose_a=150 (was 100)` (the Animalia elk is 100 on its Monster and its placeholder alike, so it needs no write); the rgl log shows one "The
'taom_body_length' attribute is not declared" line per sized Monster and no "required" line for `body_length`, plus
the re-read's `opening <path>` lines; each animal is the size it was before the move; the moose's antlers reach
what they strike.

## Performance

Once per game init: one merged read of the Monsters XML (the same files the engine just loaded) and one pass over the
items. Per scan of a creature that opts in: one native `AgentScale` read, about five scans a second per ridden animal.

## Changelog

- 2026-09-23: created (#646). Sizes moved from the three Horse items onto their Monsters; the great elk's and the
  Animalia animals' `AuthoredScale` constants and their size pins deleted. Final review: a warning for a size no item
  names, the recompute's failure reported instead of thrown, the three reflection targets in the binding gate, the
  reach gate simplified to a positive requirement, the recipe parity test.

## GitHub Issue

- **Issue:** part of [#646](https://github.com/haterade22/TAOM/issues/646)
- **Status:** built and unit-tested; in-game checks owed
