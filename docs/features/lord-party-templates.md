# Lord Party Templates

## Overview

A named lord fields a party template of his own instead of the one his clan binds. Faramir raises
Ithil Guard rangers with a Minas Tirith horse; Sauron raises Uruks and Black Numenoreans with no
lackeys. The map is one JSON file, hero id to template id, and it reaches the engine through a
Harmony seam on the clan template getter that is live only while a lord party is being built.

## Why This Exists

- **Vanilla behavior:** a lord's roster comes from `Clan.DefaultPartyTemplate` (installed v1.4.8,
  `Clan.cs:112-122`): the clan's `default_party_template` binding, else the culture default. Both
  reads that draw a lord's troops go through it. `LordPartyComponent.InitializationArgs.InitializeLordPartyProperties`
  (`LordPartyComponent.cs:38`) builds the initial roster on every creation path, and
  `HeroSpawnCampaignBehavior.SpawnLordParty` (`:265`) weights the new-game top-up over the same
  template's stacks. There is no per-hero field anywhere: `PartyTemplateObject.Deserialize` reads
  only `id`, and no `Hero` or `NPCCharacter` attribute names a template.
- **TAOM requirement:** Faramir (`lord_1_34`) shares `clan_empire_west_1` with Denethor, Boromir,
  Hurioneth and Nemos, so rebinding the clan would put rangers under the Steward and the Captain of
  the White Tower. Sauron (`lord_1_17`) owns `clan_empire_south_3` (Melkondili) with the Black
  Numenorean lords Herumarth and Naktharil, who keep the orc roster by an earlier decision
  ([black-numenorean.md](black-numenorean.md)). A new clan for Faramir would need a home
  settlement, banner and heraldry and would split him from his father's house. Only a per-hero
  override changes exactly the two lords asked for.
- **Without this feature:** Faramir spawns the Anorien levy and Citadel roster of
  `kingdom_hero_party_gondor_minas_tirith_template`; Sauron spawns orc lackeys, warg riders and
  militia from `kingdom_hero_party_mordor_empire_south_3_template` with a 4% Black Numenorean
  sprinkle.

## Architecture

### Design Challenge

The template is chosen inside two engine methods that read a clan property, not a hero property.
A transpiler on each read is fragile across engine bumps. Rebuilding the roster after the fact
would duplicate the engine's fill formula (one ratio per party, `min + (max - min) * r` per stack,
then a weighted top-up), and drift with it.

### Solution Approach

Patch88, three thin patches in one category, plus a pure decision and a validating config loader.

- **Postfix on the `Clan.DefaultPartyTemplate` getter.** If a thread-static ambient owner is set
  AND the clan being read is that owner's clan AND the JSON maps that owner, the result is swapped
  for the mapped `PartyTemplateObject` (resolved through `MBObjectManager`). With no ambient owner
  the postfix returns on its first line, so the other readers of that getter
  (`Clan.HasNavalNavigationCapability`, `ClanVariablesCampaignBehavior`, the bandit and
  caravan-ambush spawns) pay one static read.
- **Scope prefix and finalizer on `HeroSpawnCampaignBehavior.SpawnLordParty(Hero, bool)`.** The
  prefix saves the previous ambient owner in Harmony's per-invocation `__state` and sets `hero`;
  the finalizer restores `__state` and returns the exception it was given, unchanged. That last
  part matters: Patch65 finalizes the same method and swallows `InvalidOperationException` on
  purpose, and every finalizer on a method writes one shared exception slot, so this one has to be
  transparent or it would decide what Patch65 decided.
- **Scope prefix and finalizer on `LordPartyComponent.InitializationArgs.InitializeLordPartyProperties(MobileParty, Hero)`.**
  Same shape around `owner`. This is the one place every lord party's first roster is drawn, so
  marking the owner here covers `HeroSpawnCampaignBehavior`, `RebellionsCampaignBehavior`,
  `CompanionRolesCampaignBehavior` and StoryMode's `DefeatTheConspiracyQuestBehavior` in one go.
  The two scopes nest (`SpawnLordParty` reaches the initializer through
  `MobilePartyHelper.SpawnLordParty`), which is why the finalizers restore rather than clear.
- **The decision is pure.** `LordPartyTemplateResolution.Resolve(ambientOwnerId, ambientOwnerClanId,
  readClanId, lookup)` returns the template id or null, so every branch that decides WHETHER a read
  is overridden is unit tested without a campaign.
- **What it never touches.** A read of any clan other than the owner's; any owner the JSON does not
  name (his clanmates keep the clan binding); the rebel branch, which reads
  `Culture.RebelsPartyTemplate` instead; and the player's clan, for which
  `InitializeLordPartyProperties` (:32) takes the position-only path before any template is read,
  so a Player Switcher takeover of Faramir is unaffected by construction.
- **Failure.** A mapped id the object manager cannot resolve leaves vanilla's answer in place and
  warns once per hero. A typo is a lord on his clan roster plus a log line, never a null template
  into `FindAppropriateInitialRosterForMobileParty`.
- **The window is the whole call.** The ambient owner is set for the duration of `SpawnLordParty`
  and of `InitializeLordPartyProperties`, not for the one statement that reads the template. Any
  read of the owner's clan template that runs synchronously inside either sees the swap. In the
  installed v1.4.8 engine nothing else does (the review decompiled the chain: `MobilePartyCreated`
  fires inside `MobileParty.CreateParty` while the scope is open and none of its three listeners
  reads the getter; `LordPartyComponent.CanHaveNavalNavigationCapability` is overridden to `true`,
  so the clan's naval capability is never consulted there). A future listener or another mod's
  hook that reads the getter inside that window would see the override; re-check this on an engine
  bump alongside the binding tests. One more theoretical residual: two lords of the SAME clan
  spawning on the same call stack would let the inner lord's read match the outer lord's mapping.
  No engine path does that today (re-entrancy was traced through `RemoveGovernorOf` and the
  `MobilePartyCreated` listeners).

### Component Diagram

```
lord_party_templates/lord_party_templates.json
        |
  LordPartyTemplateConfigProvider (validating loader, Lazy, process lifetime)
        |
  LordPartyTemplateService (hero id -> template id)
        |
  LordPartyTemplateResolution.Resolve (pure: ambient owner + clan match + lookup)
        |
  Patch88_LordPartyTemplate (postfix on Clan.DefaultPartyTemplate, MBObjectManager lookup)
        ^                                ^
  Patch88_SpawnLordPartyScope      Patch88_InitializeLordPartyPropertiesScope
  (new-game top-up)                (initial roster, every creation path)
```

## Configuration

### Config File: `Main/_Module/ModuleData/lord_party_templates/lord_party_templates.json`

| Field | Type | Description |
|-------|------|-------------|
| `overrides` | object | Hero StringId to party template StringId, both bare. `lord_1_34`, never `Hero.lord_1_34`; `kingdom_hero_party_gondor_faramir_template`, never `PartyTemplate.kingdom_hero_party_gondor_faramir_template`. A prefixed id is dropped with a warning at load. |

The provider is `Reuse.Singleton` and reads the file once per process: retuning needs a full
application restart, not a new campaign. A missing file, an unparseable file, a null map, a blank
hero id, a blank template id and a prefixed id all fail soft with a log line and leave that lord on
his clan roster. Whether the hero or the template exists is NOT checked at load (the object
manager is empty then); the patch checks the template at spawn and warns once per hero, and
`ShippedLordPartyTemplateTests` checks both offline.

**Keep every template named here unbound by any clan or culture.** A clan binding would hand it to
every lord of that clan, which is the outcome the override exists to avoid.
`ShippedLordPartyTemplateTests.ShippedConfig_TemplatesAreBoundByNoClanOrCulture` fails if one
appears in `spclans.xslt`, `characters/clans.xml`, `spcultures.xslt` or `taom_spcultures.xml`.

### Current Values

Both templates sit in `Main/_Module/ModuleData/taom_partyTemplates.xml`, sized to the culture
spawn ceilings in `tools/rebalance_party_template_maxes.py` (`CULTURE_TARGETS`: Gondor 200, Mordor
260) with min sums of 11, inside the 10 to 14 band their siblings use. The engine draws one ratio
per party and fills each stack to `min + (max - min) * r`, so the expected spawn roster is the
midpoint and the shares below are shares of the ceiling
([party-template-sizing.md](../reference/party-template-sizing.md)).

`lord_1_34` Faramir, `kingdom_hero_party_gondor_faramir_template`: 100 ranged, 40 cavalry, 60
infantry (the brief was 50 / 20 / 30).

| Troop | Level | Group | min / max |
|---|---|---|---|
| `gondor_ith_longbowman` | 36 | Ranged | 3 / 35 |
| `gondor_ith_sharpshooter` | 41 | Ranged | 2 / 30 |
| `gondor_ith_moon_guard` | 46 | Ranged | 1 / 20 |
| `gondor_ithilien_ranger` | 51 | Ranged | 0 / 15 |
| `gondor_ith_watcher` | 26 | Infantry | 2 / 20 |
| `gondor_ith_veteran` | 31 | Infantry | 1 / 15 |
| `gondor_ith_sergeant` | 36 | Infantry | 1 / 15 |
| `gondor_ith_captain` | 41 | Infantry | 0 / 10 |
| `gondor_ano_mt_cavalry` | 21 | Cavalry | 1 / 15 |
| `gondor_ano_mt_heavy_cavalry` | 26 | Cavalry | 0 / 15 |
| `gondor_ano_mt_knight` | 31 | Cavalry | 0 / 10 |

Ithilien has no mounted line, so the horse is Anorien (Minas Tirith), the fief his clan holds. The
older orphan `kingdom_hero_party_gondor_ithilien_template` (Ithil Guard plus a Blackroot bow, bound
by nothing since 2026-08-14) is left as it was.

`lord_1_17` Sauron, `kingdom_hero_party_mordor_sauron_template`: 104 low tier, 78 middle, 78 high
(the brief was 40 / 30 / 30). Tiers by `level`: low up to 16, middle 21 to 31, high 36 and up.
"Black Uruks" is read as the `mordor_uruk_*` line; no `black_uruk` troop exists.

| Troop | Level | Tier | min / max |
|---|---|---|---|
| `mordor_orc_warrior` | 16 | low | 2 / 26 |
| `mordor_orc_impaler` | 16 | low | 1 / 20 |
| `mordor_orc_scout` | 16 | low, ranged | 1 / 16 |
| `morannon_warrior` | 16 | low | 1 / 20 |
| `mordor_uruk_fighter` | 16 | low | 1 / 22 |
| `mordor_uruk_warrior` | 21 | middle | 1 / 16 |
| `mordor_uruk_shieldbearer` | 26 | middle | 1 / 14 |
| `mordor_uruk_archer` | 26 | middle, ranged | 0 / 12 |
| `mordor_uruk_vanguard` | 31 | middle | 0 / 12 |
| `mordor_num_initiate` | 26 | middle | 1 / 12 |
| `mordor_num_infantry` | 31 | middle | 0 / 12 |
| `mordor_uruk_captain` | 36 | high | 0 / 12 |
| `mordor_uruk_baraddurguard` | 36 | high | 1 / 14 |
| `mordor_num_vet_infantry` | 36 | high | 1 / 12 |
| `mordor_num_vet_archer` | 36 | high, ranged | 0 / 10 |
| `mordor_num_warden` | 41 | high | 0 / 10 |
| `mordor_num_knight` | 41 | high, cavalry | 0 / 8 |
| `mordor_num_temple_guard` | 46 | high | 0 / 6 |
| `mordor_num_temple_knight` | 46 | high, cavalry | 0 / 6 |

Herumarth and Naktharil stay on `kingdom_hero_party_mordor_empire_south_3_template`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LordPartyTemplates/Hooks/Patch88_LordPartyTemplate.cs` | Postfix on the `Clan.DefaultPartyTemplate` getter, the ambient owner slot, `Initialize`, `ResetForUnload` |
| `Main/Features/LordPartyTemplates/Hooks/Patch88_SpawnLordPartyScope.cs` | Ambient scope around the new-game top-up |
| `Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs` | Ambient scope around the initial roster, every creation path |
| `Main/Features/LordPartyTemplates/LordPartyTemplateResolution.cs` | The pure decision |
| `Main/Features/LordPartyTemplates/LordPartyTemplateService.cs` | Hero id to template id |
| `Main/Features/LordPartyTemplates/LordPartyTemplateConfigProvider.cs` | Validating JSON loader |
| `Main/Features/LordPartyTemplates/LordPartyTemplatesIoC.cs` | DryIoc registration (both singletons) |
| `Main/_Module/ModuleData/lord_party_templates/lord_party_templates.json` | The map |
| `Main/_Module/ModuleData/taom_partyTemplates.xml` | The two templates |

Wiring: `Main/IoC.cs` registers the feature after `LordSpawnGuard`; `Main/SubModule.cs` calls
`Patch88_LordPartyTemplate.Initialize` and applies the category in the standard
`OnGameInitializationFinished` batch beside Patch65 (the same two listeners reach `SpawnLordParty`,
so the same placement argument holds), and resets it in the unload list.

## Dependencies

- `IPathService` (Core) for `ModuleDataPath`.
- `IModLogger` (Core).
- No adapter: the only engine reads are in the entry point (`Hero.StringId`, `Hero.Clan`,
  `Clan.StringId`, `MBObjectManager.GetObject`), and the service and decision see ids only.

## Tests

46 tests in `TAOM.Tests/Features/LordPartyTemplates/`:

- `LordPartyTemplateConfigProviderTests.cs` (9): missing file, malformed JSON, valid map, null map,
  blank hero id, blank template id, prefixed ids, summary warning, read-once.
- `LordPartyTemplateServiceTests.cs` (5): mapped, unmapped, null, empty, case-sensitive.
- `LordPartyTemplateResolutionTests.cs` (7): no ambient owner, another clan's read, owner without a
  clan, unknown read clan, unmapped owner (a clanmate), the hit, blank lookup result.
- `ShippedLordPartyTemplateTests.cs` (13): the JSON maps both lords, both hero ids are retagged in
  `lords.xslt` or defined in `characters/lords.xml`, both templates exist and are bound by no clan
  or culture, every stack troop has a level, no zero max or min above max, min sums in band,
  Faramir sums to 200 at 100 / 40 / 60 by `default_group` and is Ithilien on foot with Minas Tirith
  horse, Sauron sums to 260 at 104 / 78 / 78 by level band with only Uruk and Black Numenorean high
  tier and nothing under level 11.
- `Patch88LordPartyTemplateTests.cs` (12): the getter, `SpawnLordParty(hero, isNewGame)` private
  with one overload, `InitializeLordPartyProperties(mobileParty, owner)` on the nested args type,
  both vanilla bodies still call the getter, every engine member the postfix references, the
  category literal on all three classes, the `SubModule.cs` apply / initialize / reset lines,
  call presence in the postfix and both scopes, finalizer transparency, `ResetForUnload`.

Not testable offline: that Harmony hands `__state` from the prefix to the finalizer and that the
swapped template reaches `FindAppropriateInitialRosterForMobileParty`. Both are on the in-game list.

## How to give another lord his own roster

1. Author the template in `taom_partyTemplates.xml`. Size its max sum to the culture target in
   `tools/rebalance_party_template_maxes.py` (the dry run reports the delta) and keep the min sum
   near its siblings. Every `troop=` must be a troop with a `level`.
2. Add `"<hero StringId>": "<template id>"` to `lord_party_templates.json`. Bare ids on both
   sides. Vanilla-id lords retagged in `lords.xslt` use the vanilla id (`lord_1_34`).
3. Do NOT bind the template to a clan or culture.
4. Extend `ShippedLordPartyTemplateTests` with the new pair and whatever composition the brief
   asked for, then `dotnet test TAOM.Tests --filter FullyQualifiedName~LordPartyTemplate` and
   `python tools/validate_moduledata.py`.
5. Restart the game. The lord's NEXT party spawn uses the template; a party already on the map
   keeps its roster.

## Performance

The postfix runs on every read of `Clan.DefaultPartyTemplate` and costs one thread-static read
when no lord spawn is in flight. Inside a spawn it costs a dictionary lookup and, on a hit, one
`MBObjectManager.GetObject`, once per party created. The two scope patches are a field write each.

## Verification in game

Owed as of 2026-09-12 (new campaign, then a saved game):

1. Faramir's party: mostly Ithil Guard ranged, Ithil Guard foot, Anorien horse, no Anorien peasants.
2. Sauron's party: Uruks and Black Numenoreans, no orc lackeys or warg tamers.
3. Denethor, Boromir, Herumarth and Naktharil unchanged.
4. A loaded pre-feature save keeps every existing roster; only a respawn (after capture and release,
   or a destroyed party) picks up the template.
5. `taom_debug_*.log` shows `Loaded lord_party_templates.json, 2 lord override(s)` and no
   `[LordPartyTemplates]` warning.

## Changelog

- 2026-09-12: feature shipped, Faramir and Sauron mapped (#580).

## GitHub Issue

- **Issue:** #580, [Per-hero lord party templates](https://github.com/haterade22/TAOM/issues/580)
- **Status:** Open (in-game verification owed)
