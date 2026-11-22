# Story Missions

## Overview

Story Missions are one-shot battles that open inside a loaded campaign and run on the Long Defeat's
run machine ([long-defeat.md](long-defeat.md)). Slice 1 is built: a dev console command opens a
defence of one of seven fortresses, and the mission writes nothing to the campaign. The mission can
play its own music track, and vanilla music stays quiet for that mission only. Slice 4 is built: the
War Chronicle's Battle of the Hornburg offers the Helm's Deep defence to the player, and the result
decides the event. A win holds Helm's Deep; a loss gives it to Isengard. Slice 2 is built: two missions on
a field scene play the Breaking of the Fellowship at Parth Galen, one for each side, and each is decided
by whether Boromir falls. Slice 3 is built: the War Chronicle's Breaking of the Fellowship offers those
two missions, and the result decides whether Boromir lives.

## Why This Exists

- **Vanilla behavior:** a campaign battle needs a map event and changes the campaign (casualties,
  prisoners, renown). Custom Battle runs outside any campaign.
- **TAOM requirement:** set-piece battles from the War of the Ring (Helm's Deep, the White City,
  Osgiliath) that a campaign can offer to the player, with authored waves and their own music.
- **Without this feature:** the only way to play a scripted defence is the Long Defeat from the main
  menu, with no campaign around it.

## Architecture

### Design Challenge

A mission that opens from the campaign map must not look like a campaign battle to other features,
and it must not change the campaign. The engine also needs a mission screen with a working order
bar and a player who can take a soldier after death.

### Solution Approach

- **Entry:** `taom.story_open <mission> [auto]` (`StoryMissionCheats`) opens the menu
  `taom_story_dev` (`StoryDevMenuBehavior`). Its "Open the mission" option is a
  `LeaveType.Mission` option, so the mission opens from a menu click, never from a console call or
  a campaign tick. When the mission ends, the engine returns to this menu.
- **Refusals** (`StoryDevMenuBehavior.Open`): a co-op session, enlistment, an encounter or a map
  event, any other open menu, an empty catalog and an unknown id. Each refusal prints one line in the
  console.
- **Launch** (`StoryMissionLauncher`): `MissionState.OpenNew` with the Long Defeat's siege behavior
  list and no map event, as Host Review opens its mission. Each launch gets its own
  `LongDefeatSession`. It never assigns `Game.PlayerTroop`, which in a campaign is the player's hero.
  The launcher refuses a missing culture and a missing troop, and logs why. A row with side `Attack`
  makes the attacker combatant the player's team (#1409).
- **Run plan** (`StoryPlanBuilder`): the row's garrison, waves, rules and free heroes. There is no shop, gold,
  fortification, affix, boon or class, and the run stops after the authored waves.
- **Verdict** (`StoryVerdict`, `StoryEndWatch`): the run's side-neutral end reason becomes a win or a
  loss for the player's side. A stalemate, a fault or no result decides nothing. About five seconds
  after the result, the mission ends and the player returns to the map.
- **Check lines:** the hosted run writes its Long Defeat check lines through `HostedRunOutcomeLog`,
  which renames `taom.long-defeat.*` to `taom.story-missions.hosted.*`. Thus a story run never lands
  on the rows of `tools/ingame_checks/1079.toml`, whose HUD rows would read as fails.
- **Music** (`StoryMusicCue`, `MissionMusicAdapter`, and in TAOM.Client `StoryMusicMissionView`):
  the track starts when the scene first renders, loops until the mission ends, and is released by both
  teardown hooks. `MusicPauseGateTests` gates the music pause sites.

### The story offer (slice 4)

- **Source:** a War Chronicle siege with a `story` block (`war_chronicle.json`; the Hornburg names
  `helms_deep_defend`). The block may also name an `attackMission`, the same battle from the
  attackers' side (#1326); no shipped window has one yet, because the maintainer has not decided the
  key (#1409 step 6). `IChronicleStoryPort.GetStoryOffers` lists each Active story window that the
  player has not declined, that no attacker has besieged, and whose target the defender still holds
  (read live), while no chronicle announcement waits to show ([war-chronicle.md](war-chronicle.md)
  "The event chain", "Story offer"). The MCM switch "Offer Story Missions" (War of the Ring/War
  Chronicle) and War Ended empty the list.
- **Gates** (`StoryOfferService.NextOffer`): single-player authority only (no co-op session, no
  dedicated server), not enlisted, not a prisoner, not in a map event (the offer opens a menu, and
  only the battle menu may close the player's battle), a `mission` the catalog loads as a defence
  and an `attackMission` it loads as an attack (else one warning per window and id, and that side is
  not offered), a window not snoozed today, and at least one side the player may take.
- **Sides** (#1326, `StorySideGate.Choose`): the player never fights for a side of the opposite
  alignment. His side is `IAlignmentService.ResolveSide` of `FactionSides.OfPlayer()` (his kingdom,
  or his clan outside one, then that faction's culture). The defenders' side is `GetKingdomSide` of
  the window's defender kingdom and the attackers' side that of its first attacker kingdom, so a war
  pledge counts; there is no culture fallback, and `ShippedStoryOffersTests` requires every kingdom
  of a story window to be listed in `execution/alignment.json`. A neutral player may take either
  side, and a neutral side is open to everyone. At Helm's Deep (Rohan free, Isengard evil) a free player may defend, an
  evil player may attack, and a neutral player may do either. His own kingdom comes first: when it
  is the window's defender he may only defend, and when it is one of the attackers he may only
  attack, so he never fights against his own realm. A window with no side for him is
  not offered at all: "Let fate decide" is a saved decline, and a player with nothing else to click
  would close the window for good. So until an attack mission ships, an evil player and a player of
  Isengard or Dunland get no Helm's Deep offer.
- **Offer** (`StoryOfferBehavior`, `StoryOfferPopup`): every 30 campaign ticks while time runs, on
  a free map (`MapPopupGate.IsMapFree`), a two-button popup whose question follows the sides on
  offer (`StoryOfferText`). "Ride to Helm's Deep" ("March on Helm's Deep" for a player who may only
  attack) opens the menu
  `taom_story_offer` if the map is still free (`MapPopupGate.IsMapFreeUnderPopup`: the rest of the
  tick can open an encounter or a conversation under the popup). "Not now", or Escape, snoozes the
  offer until the next day, as does a popup closed with no answer. The menu has "Defend the walls"
  and "Storm the walls" (`LeaveType.Mission` options that call `StoryMissionLauncher.Launch` with
  the window's `mission` or `attackMission` and a verdict callback; each shows only when the player
  may take that side) and "Let fate decide", which declines (saved by the chronicle as `sd`). None
  of the three is a leave option, so no menu hotkey declines by accident.
- **Result:** the engine returns to the menu after the mission and runs its init again
  (`MapState.OnActivate`, `MenuContext.Refresh`). That init applies the verdict through
  `StoryOfferService.Apply` once, before the player can save (the behavior saves nothing), and
  "Continue" only leaves. The verdict is read for the side the player took. Held walls (a won
  defence, or a lost attack) resolve the window Held (`ResolveStoryHeld`: the owner check first,
  nothing after War Ended). Fallen walls (a lost defence, or a won attack) call `ConfirmStoryWindow`
  first, which runs the same owner check and War Ended guard, then
  `IStoryCampaignAdapter.GiveSettlement`, and the chronicle's owner check resolves Fell. A lost
  attack closes the window Held on purpose: a window left open would let the player storm the walls
  again every day. Undecided (a fault, a stalemate, a mission left before the run starts) and a failed
  transfer snooze the offer until the next day. A retreat after the run starts is Lost (slice 1's
  `StoryVerdict` for a defender), so it gives the fortress away. The menu text follows the applied
  result and the side (`StoryOfferText.Menu`).
- **Check lines:** `taom.story-missions.offer.shown` and `.offer.result`. `OutcomeLog` writes one line
  per key for the whole process, so each key names the campaign (`UniqueGameId`), the window and,
  for a result, the verdict (`AttackWon` and `AttackLost` for the attacking side): a reload that
  plays the other result still writes its line. The shown line carries `choices=` (Defend, Attack or
  Defend+Attack) and `side=` (the player's resolved side).
- **The transfer** (`StoryCampaignAdapter`): `ChangeOwnerOfSettlementAction.ApplyByDefault` to the
  ruler of the first listed attacker with a living ruler, then the old garrison roster is emptied,
  the militia set to 0 (as vanilla's rebellion transfer does, `RebellionsCampaignBehavior`) and
  `Town.LastCapturedBy` set to the taker's clan. Not `ApplyBySiege`: it destroys the garrison through
  `capturerHero.PartyBelongedTo.Party` (v1.5.7 `ChangeOwnerOfSettlementAction.cs:34-37`), which
  throws for a ruler with no party, and it opens a fief claim. `TroopRoster.Clear` removes each entry
  through `AddToCountsAtIndex`, which updates the roster version and detaches a hero, and the
  garrison recruits again (`GarrisonRecruitmentCampaignBehavior`). `LastCapturedBy` is read by
  `SettlementClaimantDecision.CalculateMeritOfOutcome` (30 merit for the capturing clan in a later fief
  vote), which `TaomSettlementClaimantDecision` keeps. The taker may be a prisoner (the ruler's clan
  takes the fief), and need not be at war with the old owner.
- **Owner-change readers** (audited 2026-11-13 for a Default transfer with no capturer; the event
  fires once): ArmyTargeting clears its reach cache; Hold the Walls returns early (not by siege); a
  career quest gives no credit; Culture Conversion starts its pending conversion to Isengard, which
  can later replace the notables; Fief Granting forgets any siege record; Namegiving reconciles the
  lore name; Realm Borders redraws; Realms keeps the title with its Rohan holder (`TransferKind.Other`)
  and adds no claim; Siege Defence ends any event at the town; Staged Siege logs only a recorded
  town; the War Chronicle resolves Fell. In vanilla (v1.5.7), Default applies no relation penalty to
  the old owner and opens no fief vote. Its listeners also:
  - capture heroes without a party who are at war with the new owner, except notables, wanderers,
    prisoners, the dead and the player, and send hostile lord parties out
    (`PrisonerCaptureCampaignBehavior`);
  - reconcile other parties and siege camps: a siege of the town dissolves, and a party at war with
    the new owner leaves (`PartyDiplomaticHandlerCampaignBehavior`);
  - remove every kingdom's fief votes on the town, and move a former player owner's stash into the
    town's market (`SettlementClaimantCampaignBehavior`);
  - release hero prisoners who are at peace with the new owner (`PrisonerReleaseCampaignBehavior`);
  - remove the governor and the patrol party, and recalculate clan homes and the garrison wage limit
    (`ChangeGovernorAction`, `PatrolPartiesCampaignBehavior`, `ClanVariablesCampaignBehavior`).

### The free heroes (#1366, Phase 6 of #1325)

Aragorn, Legolas and Gimli stand with the garrison in `helms_deep_defend`. They cost nothing and are
not for sale; each is a row of the mission's `heroes` list.

- **Data:** `heroes` rows are `id`, `troop` and an optional `healthFactor` (1 to 10, 1 when absent): the
  shop's hero row without a price. `StoryHeroesReader` reads the list through `HeroesReader.ReadRow`,
  the row reader the Long Defeat shop uses, so `hero=aragorn` means the same in both modes. A missing
  key is no hero and no warning; a value that is not an array gives no hero and one warning; a bad or
  repeated row is skipped with a warning; past three valid rows (`StoryHeroesReader.MaxHeroes`) the
  rest are dropped with one warning. `helms_deep_defend` ships all three with `healthFactor` 8.0 (#1438), the
  factor of `shop.json`; `ShippedStoryMissionsTests` requires the two to agree.
- **Plan:** `StoryPlanBuilder` sets `RunPlan.FreeHeroes` from the definition. `SurvivalRun.Begin`
  queues each free hero as pending (`QueueFreeHeroes`), so he holds one place under the garrison cap
  and the defenders-dead check waits for him. The first director tick spawns them, after the lord has
  been adopted, at the plan's home place (the mission's `muster` foot point), in the Long Defeat's
  formation 7 (order key 8) with the wall order and the hold. A free hero who falls or flees is not
  for sale: `TryBuyHero` answers `UnknownHero`, because the story shop is empty. A spawn the field
  refuses pays nothing back.
- **Launch check:** `RunPlanTroops.AllTroopIds` includes the free heroes' troops, so the launcher's
  missing-troop refusal names a hero troop that did not load.
- **The characters and the item in a campaign:** `SubModule.xml` loads
  `characters/taom_mission_heroes` and `hero_abilities/hero_abilities_items` for Campaign,
  CampaignStoryMode and CustomGame. The characters have no `<Hero>` entry, `occupation="Wanderer"` and
  no `is_template`, so the companion pool and the encyclopedia do not list them; they are obsolete, so
  no picker does. The arrow item is not merchandise and cannot be picked up, so workshops, loot and
  prizes skip it. `level="36"` gives them battle tier 7 with no Hero
  (`BasicCharacterObject.GetBattleTier`).
- **No `IsHero` in a campaign:** `CharacterObject.IsHero` is true only with a `Hero` behind it, so the
  three read `IsHero == false`. The adapter therefore passes `FirstBattleEquipment` as the spawn kit,
  because the engine fixes a hero's equipment only for `IsHero` and otherwise rolls item modifiers in a
  campaign. For the same reason the adapter puts each hero's own body and age on the build data, so the
  story mission's face cache cannot give him a garrison soldier's face. A fallen player can take over a
  hero there; the hero keeps his own health, counts as near his place in the `hunters` line, and his
  later death is booked as his fall and as the player's death. The detail for abilities, take-over,
  faces and the aura is in [hero-abilities.md](hero-abilities.md) and [hero-auras.md](hero-auras.md).
- **Check lines:** the hero spawn line reads `taom.story-missions.hosted.hero` with
  `is_hero=0 campaign=1`; the `hunters` line is `taom.story-missions.hosted.hunters`. The ability,
  ranged and aura lines keep their own ids, because only the Long Defeat's lines are renamed.
  `taom.story-missions.roster` is written once per process when a campaign session starts (a new game
  or a loaded save, through `StoryRosterBehavior` and `StoryRosterService`): the troop ids of every
  mission's plan and the extra arrow item of every hero kit that resolve to nothing. It is a log line
  at session start, so a new game and a loaded save in the same process write one line, not two.
- **Not done:** no hero strip on the HUD, no message when a hero falls, no per-hero position (the three
  stand where the heroes' home place puts them, at the nearest wall lane), no `<Hero>` entry, and no
  unattended driver beyond the one defence mission of #1368 ([scenario autorun](scenario-autorun.md)).

### Field missions (slice 2)

A row with a `field` block runs on a field scene with no walls. The run machine is the same; four
things differ, and each has a run behind it.

- **The list and the boot.** `StoryMissionLauncher.CreateFieldBehaviors` is the siege list without
  `SiegeMissionPreparationHandler` and `MissionSiegeEnginesLogic`, with the field team AI and
  vanilla's hard border. `AmmoSupplyLogic` for the defenders stays, though vanilla's field lists
  have none: Legolas's quivers refill as they do at Helm's Deep, and both missions were balanced
  with it. The director breaks no gate, deploys no engine, reads no lane and takes no
  keep from the scene. The boot line ends `siege_ai=false ... field=true`, and it fails when a field
  row runs under siege team AI.
- **Start places.** A field run builds no deployment plan. Each formation is positioned at its
  side's start place before every batch, and each soldier spawns into his own formation: the
  mechanism the recorded muster places use on a siege. A place is the row's recorded one, else a
  point of the scene's spawn path, 40 m to each side of the path's pivot
  (`LongDefeatBattlefieldAdapter.Field.cs`). A place with no navmesh, or outside the mission
  boundary, is no place, and a mission with no place for a side does not start. The part of a wave
  that spawns on a later tick is sent to the start place again: without that, 4 of 12 skirmishers
  spawned more than 25 m from it, beside the defenders (run `E:/Temp/scenario-runs/20261121-183158`).
  A soldier who spawns into his own formation takes the formation's order position at that moment;
  that the ranged formation had moved it toward the enemy is inferred from the run, not read in the
  engine. The spawn check of a field run measures ground distance (`far` is more than
  25 m from the start place).
- **Two sides and groups.** The attackers of a defence have a second start place on the far side
  of the defenders. Each new group starts at the place farther from where the defenders stand
  (`FieldPlaces.AttackersFartherFrom`), so no group appears inside the fight. A field row may set
  `rules.liveCap` below the siege floor of 101, and attackers after the opening join in groups of
  half the live room, which is the smaller of that cap and the battle size less the standing
  defenders (`SurvivalRun.FieldGroupPart`). A whole wave that arrives together kills a hero in
  about five seconds: Aragorn took five head hits of 70 to 79 in two seconds (run `20261121-183616`).
- **Free heroes.** On a field the heroes of either side stand in their class formations under
  the team AI (the spawn line ends `group=class`). Held at a post, as on a wall, they do not fight:
  Aragorn landed 4 hits in 6 minutes (run `20261121-184542`). The player can still order
  formations I and II.

**The hero who must live.** `mustLive` names one hero of the row. His fall ends the run with
`RunEndReason.HeroFell`: a loss for the side that defends him and a win for the side that attacks
(`StoryVerdict`). A rout is no fall, so a hunt in which Boromir flees and the other three die is still
won; whether that should count as his escape is an open decision. A stalemate charge on a field sets
only the attackers to charge and hands the player's formations back to the AI; the defenders' tactic
list is left alone, because the engine offers no way to restore it. When the field refuses his spawn the run ends as a stalemate,
which decides nothing. A field row may have no garrison when it has heroes, and at most four heroes.

**Boromir.** `taom_hero_boromir` (`characters/taom_mission_heroes.xml`) is a copy of the lord
`lord_1_75`: the same face, skills and name key, his battle kit without the horse and the helmet. The
campaign hero never stands behind the mission agent, so the mission writes nothing to him.

**Numbers, from the unattended runs of 2026-11-21.** In `parth_galen_save` the bot as the player
adds almost nothing, and the Fellowship then clears waves 1 and 2 and kills 86 of 90 before Boromir
falls in wave 3 (run `20261121-190746`, 9 minutes). The mission is therefore lost narrowly without
the player. `parth_galen_kill` was lost with a band of 12 and of 18 (run `20261121-191916`) and won in 41 s with 25, so the band is 20.
No person has played either mission yet.

### The hero story (slice 3)

The Breaking of the Fellowship is a Narrative event of the War Chronicle. With a `story` block that
names a `hero`, it opens a window of 10 days and offers the two Parth Galen missions; its result is
the life of Boromir (`lord_1_75`).

- **The window** (`ChronicleService.HeroStoryCanOpen`): it opens only where a story mission can run
  and can matter: a single-player campaign, the setting "Offer Story Missions" on, a hero who lives
  and is not the player (Player Switcher), and kingdoms this campaign has. Anywhere else the event
  occurs at once as text, as before, and nobody dies. The window has no opening announcement,
  because a queued announcement holds every offer back; the offer popup tells the player.
- **Sides** (#1326, unchanged): the event's `defender` is Gondor (`empire_w`) and its `attackers`
  are Isengard. A free player may stand with the Fellowship, an evil player may lead the Uruk-hai,
  a neutral player may do either, and nobody fights his own realm.
- **Results** (`StoryOfferService.ApplyHero`, `IChronicleStoryPort.ResolveHeroStory`): the hero
  lives when his side won, whichever side the player took. He lives: the event resolves `Averted`
  (a new state, saved as 7) with its own line and effects. He dies: the chronicle kills him and the
  event resolves `Occurred`, as the book has it. "Let fate decide" and a window nobody answers in
  10 days both end as `Occurred`, with the kill. A mission with no verdict snoozes the offer to the
  next day.
- **The kill** (`ChronicleWorldAdapter.KillHeroInBattle`): `KillCharacterAction.ApplyByBattle` with
  no killer. The result is read back from the hero: Killed, Deferred or Refused, and the chronicle's
  log line names it. The engine only marks a hero whose party is in a battle or besieges. The end
  of that battle kills him (`MapEventSide.HandleMapEventEndForPartyInternal`). A mark made while he
  only besieges is finished by the next daily tick or, with the life and death cycle off, at the end
  of his next battle. The engine handles his party, his clan and, if he rules, his kingdom.
- **Who Boromir is at that moment:** dead already, or dead during the window: the event occurs as
  text. The player himself: no window. A prisoner, a party leader, an army leader, a clan leader or
  a ruler: the engine's own death path applies, as for any lord who dies in battle.
- **Co-op:** no window, no offer and no kill.
- **A change while the window is open** (`ChronicleService.HeroStoryMatters`): a campaign that
  becomes a co-op session, a player who turns "Offer Story Missions" off, and a player who becomes
  Boromir get no offer, and the window ends as text on its close day, with no kill.
- **Menu:** the offer menu shows "Stand with the Fellowship" and "Lead the Uruk-hai" for a hero
  story, in place of the two wall options. The text is the Breaking's own (`StoryOfferText`,
  keys `taom_story_breaking_*`); it names no `{TARGET}`.

The feature declares no Harmony patch and no GameModel. Its one saved value, a declined offer, is a
field of the War Chronicle's payload.

### Component Diagram

```
story_missions/story_missions.json
        |
  StoryMissionCatalog (validating loader, Long Defeat readers)
        |
taom.story_open -> StoryDevMenuBehavior (menu, refusals)
        |
  StoryMissionLauncher (MissionState.OpenNew, StoryPlanBuilder)
       / \
      /   \
LongDefeatMissionLogic     StoryMissionLogic (StoryEndWatch, StoryVerdict)
(HostedRunOutcomeLog)      StoryMusicMissionView (TAOM.Client, StoryMusicCue)
```

## Configuration

### Config File: `Main/_Module/ModuleData/story_missions/story_missions.json`

One row per mission under `missions`. `StoryMissionCatalog` reads the file once per process. A row
without a valid id, scene, side, culture, garrison or roster is skipped with one warning. The Long
Defeat readers own the numbers (rules, keep, muster points, waves), so a story row and a fortress row
mean the same thing by the same key.

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | The id that `taom.story_open` takes |
| `name` | string | `{=KEY}English` display name |
| `scene` | string | The scene the mission opens |
| `side` | string | `Defend` or `Attack`: the side the player fights on |
| `defenderCulture`, `attackerCulture` | string | Culture ids of the two sides |
| `music` | string | Optional: an `.ogg` path under the module's `ModuleSounds` folder |
| `rules` | object | Long Defeat rule numbers, for example `preparationSeconds` |
| `garrison` | array | The defenders' troops (the player's in a Defend row): `troop`, `class`, `count` |
| `muster`, `keep` | object | Optional recorded points of the scene |
| `heroes` | array | Optional free heroes, at most four: `id`, `troop`, optional `healthFactor` (#1366) |
| `field` | object | Optional. Present: the mission runs on a field scene. May record `defenders`, `attackers` and `attackersBehind`, each `x, y, z, fx, fy`; a place it does not record comes from the scene's spawn path |
| `mustLive` | string | Optional. The `id` of one hero of `heroes` whose fall ends the mission. A value that names no hero of the row skips the row |
| `roster.waves` | array | Each wave: `count` and a weighted `mix` of troops |

### Current Values

The file has ten rows: seven siege defences, `helms_deep_attack`, and the two Parth Galen field missions. Every siege row has one wave.
Only Helm's Deep names a music track (`StoryMusic/isengard_vs_rohan.ogg`). Four rows use stand-in scenes after one-wave probes on
2026-11-10.

| Id | Scene | Defender, attacker | Note |
|----|-------|--------------------|------|
| `helms_deep_defend` | `taom_rohan_castle_helms_deep_forceatmo` | `vlandia`, `isengard` | Has muster points; free heroes Aragorn, Legolas and Gimli (#1366) |
| `minas_tirith_defend` | `taom_gondor_town_minas_tirith_forceatmo` | `gondor`, `mordor` | |
| `osgiliath_defend` | `taom_gondor_town_osgiliath_e_forceatmo` | `gondor`, `mordor` | |
| `pelargir_defend` | `taom_gondor_town_dolamroth_forceatmo` | `gondor`, `umbar` | Stand-in: the Pelargir scene took over 17 minutes to load |
| `dale_defend` | `sturgia_town_g` | `sturgia`, `khuzait` | Stand-in scene |
| `lorien_defend` | `battania_town_a` | `lothlorien`, `dolguldur` | Stand-in scene |
| `north_defend` | `empire_town_c` | `arthedain`, `gundabad` | Stand-in scene |
| `helms_deep_attack` | `taom_rohan_castle_helms_deep_forceatmo` | `vlandia`, `isengard` | Attack: the player's host of 250 (#1409). No window offers it |
| `parth_galen_save` | `battle_terrain_001` | `gondor`, `isengard` | Field. Waves of 20, 30 and 40, at most 10 alive; Boromir must live. Stand-in scene |
| `parth_galen_kill` | `battle_terrain_001` | `gondor`, `isengard` | Field, Attack: the player's band of 20; won when Boromir falls |

`vlandia` is Rohan and `sturgia` is Dale in TAOM ([id cheatsheet](../modding/id-cheatsheet.md)).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/StoryMissions/StoryMissionsModule.cs` | Feature module: the catalog, the launcher and the dev menu behavior |
| `Main/Features/StoryMissions/Config/StoryMissionCatalog.cs` | Validating loader for the JSON |
| `Main/Features/StoryMissions/Domain/` | `StoryMissionDefinition`, `StoryPlanBuilder`, `StoryVerdict` |
| `Main/Features/StoryMissions/Config/StoryHeroesReader.cs` | #1366: the `heroes` list, through `HeroesReader.ReadRow` |
| `Main/Features/StoryMissions/StoryRosterCheck.cs`, `StoryRosterService.cs`, `Hooks/StoryRosterBehavior.cs` | #1366: the `taom.story-missions.roster` line at session start |
| `Main/Features/StoryMissions/Hooks/StoryDevMenuBehavior.cs` | The dev menu and its refusals |
| `Main/Features/StoryMissions/Hooks/StoryMissionLauncher.cs` | Opens the mission, with the siege list or the field list |
| `Main/Features/LongDefeat/Domain/FieldPlaces.cs` | Slice 2: the start places of a field run, and the pick of the attackers' place |
| `Main/Adapters/LongDefeatBattlefieldAdapter.Field.cs` | Slice 2: start places from the row or the spawn path, and the lord's spawn at his side's place |
| `Main/Features/StoryMissions/Hooks/StoryMissionLogic.cs` | The mission logic that feeds `StoryEndWatch` |
| `Main/Features/StoryMissions/StoryEndWatch.cs` | The end cue and the `taom.story-missions.ended` check line |
| `Main/Features/StoryMissions/HostedRunOutcomeLog.cs` | Renames the hosted run's check lines |
| `Main/Features/StoryMissions/StoryMusicCue.cs` | The mission's own track |
| `Main/Adapters/IMissionMusicAdapter.cs`, `MissionMusicAdapter.cs` | Engine music calls |
| `Client/Features/StoryMissions/StoryMissionViews.cs`, `StoryMusicMissionView.cs` | The mission's view list (TAOM.Client, ADR-013) |
| `Main/Features/DevConsole/Cheats/StoryMissionCheats.cs` | `taom.story_open` |
| `Main/_Module/ModuleData/story_missions/story_missions.json` | The missions |
| `Main/Features/StoryMissions/StoryOfferService.cs` | Slice 4: the offer's gates and the verdict's campaign result |
| `Main/Features/StoryMissions/Domain/StorySideGate.cs` | #1326: the sides a player may take (`StorySideGate`, `StoryChoices`, `StoryTie`) |
| `Main/Features/StoryMissions/StoryOffer.cs` | An open window with the sides on offer and the player's resolved side |
| `Main/Features/StoryMissions/StoryOfferText.cs` | The popup and menu lines by sides on offer, side taken and result |
| `Main/Features/StoryMissions/Hooks/StoryOfferBehavior.cs` | Slice 4: the menu `taom_story_offer`, its two side options and the result click |
| `Main/Features/StoryMissions/Hooks/StoryOfferPopup.cs` | The popup, and a line with {TARGET} filled in |
| `Main/Adapters/IStoryCampaignAdapter.cs`, `StoryCampaignAdapter.cs` | Slice 4: captivity, the player's side pair, and the transfer of a fallen fortress |
| `Main/Features/WarChronicle/Events/IChronicleStoryPort.cs` | Slice 4: the chronicle seam (`ChronicleService` implements it) |
| `tools/ingame_checks/1105.toml` | The in-game check manifest |
| `Main/Features/WarChronicle/Events/ChronicleService.cs` | Slice 3: the hero story's window, its fate and its result (`HeroStoryCanOpen`, `ResolveHeroStory`) |
| `Main/Adapters/IChronicleWorldAdapter.cs`, `ChronicleWorldAdapter.cs` | Slice 3: whether the hero lives, whether he is the player, and the kill |
| `tools/ingame_checks/1366.toml` | The manifest of the free heroes |
| `tools/ingame_checks/1370.toml` | The manifest of the free heroes' formation churn (#1370, open): `moved_out=0` on the hunters line |

## Dependencies

- The Long Defeat (`Main/Features/LongDefeat/`): the run machine, its config readers and
  `LongDefeatMissionLogic`.
- `IGameMenuAdapter`, `IEncounterAdapter` (Adapters): the menu and the encounter state.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the co-op refusal. The co-op row
  is in [coop-interop.md](coop-interop.md).
- `IEnlistmentStateQuery` (Enlistment): the enlistment refusal.
- `IAlignmentService` (Execution) with `execution/alignment.json`, and the War Pledge override it
  reads: the player's side and the side of each kingdom of a story window (#1326).
- `IMissionMusicAdapter` (Adapters): the engine's music calls.

## Tests

`TAOM.Tests/Features/StoryMissions/`:

- `StoryMissionCatalogTests`, `ShippedStoryMissionsTests`: the loader's validation and the shipped rows, including the three free heroes.
- `StoryHeroesReaderTests`: the `heroes` list (no key, not an array, the cap of three).
- `StoryRosterCheckTests`: the roster line's rule, detail and service.
- `StoryPlanBuilderTests`, `StoryVerdictTests`, `StoryEndWatchTests`: the run plan, the verdict and the end check line.
- `StoryMissionLauncherTests`, `StoryMissionsWiringTests`, `StoryMissionWiringPinTests`: the launch refusals and the wiring.
- `HostedRunOutcomeLogTests`: the check line rename.
- `StoryOfferServiceTests`, `ShippedStoryOffersTests`, and in `TAOM.Tests/Features/WarChronicle/`
  `ChronicleStoryTests`: the offer's gates, the verdict results for both sides, the shipped story
  ids, the alignment table row of every story window kingdom, and the fate.
- `StorySideGateTests`, `StoryOfferTextTests`: the side rule (alignment and kingdom ties) and the
  lines per side.
- `StoryMusicCueTests`, and `TAOM.Tests/Infrastructure/MusicPauseGateTests.cs`: the music cue and the pause sites.
- Slice 2, in `TAOM.Tests/Features/LongDefeat/`: `LongDefeatDirectorFieldTests` (the director on a field), `Domain/SurvivalRunFieldTests`
  (the live cap and the groups), `Domain/FieldPlacesTests` (the pick of a start place), and the hero who must live in
  `Domain/SurvivalRunHeroTests`. `tools/tests/test_run_scenario.py` holds the driver's field patterns.
- Slice 3: `TAOM.Tests/Features/WarChronicle/Events/ChronicleHeroStoryTests` (the window, the fate, the kill, co-op), the hero
  story cases of `ChronicleConfigValidatorTests`, `WarChronicleSaveCodecTests` (state 7) and `StoryOfferServiceTests`, and
  `ShippedStoryOffersTests.TheBreaking_OffersTheTwoParthGalenMissions_AndDecidesBoromir`.

## How to verify in game

**Preconditions:** a loaded TAOM campaign, single player, on the campaign map with no menu open, not
enlisted. No restart and no MCM setting are needed.

1. Run `taom.story_open helms_deep_defend` (add `auto` to let the bot play the lord), then choose
   "Open the mission". The line `[Check] id=taom.story-missions.hosted.boot result=pass` proves the
   hosted run booted.
2. Let the wave end by a win or a loss. The line `[Check] id=taom.story-missions.ended result=pass`
   with `verdict=Won` or `verdict=Lost` proves the end, and the player returns to the map.
3. The `taom.story-missions.music.requested` and `taom.story-missions.music.stopped` lines prove that
   the engine accepted the track and that it looped to the end. Only a person can confirm the sound.

Slice 4, in a campaign at the Full War:

1. As a free player, or a neutral player whose realm has not pledged (`taom.print_war_pledges`),
   run `taom.chronicle_open hornburg confirm`. The popup appears on the free map, and the line
   `[Check] id=taom.story-missions.offer.shown result=pass` follows, ending `choices=Defend` and
   the player's side. The menu has no "Storm the walls". An evil player, and a player of Isengard
   or Dunland, gets no popup and no line (#1326).
2. Choose "Ride to Helm's Deep", then "Defend the walls". End the wave, then choose "Continue". The
   line `taom.story-missions.offer.result` reads `verdict=Won result=Held` or
   `verdict=Lost result=Fell taker=isengard`. `taom.chronicle_status` shows the same state.
3. On another save, choose "Let fate decide" and wait out the window with no siege: the window ends
   Held with the fate line.

`python tools/ingame_verdict.py` reads these rows from `tools/ingame_checks/1105.toml`. The proof is
owed, and Slice 2 waits on it.

Slice 2, unattended: `python tools/run_scenario.py --kind story-mission --mission parth_galen_save` (or
`parth_galen_kill`), then `python tools/ingame_verdict.py --issue 1105`. The rows scoped to each Parth Galen launch read the
field boot line, Boromir's spawn line, every attacker spawn check and the end line. Four rows stay with a person: the look of the
scene and of Boromir, the two sides the groups come from, a won defence, and a won hunt.

The proof runs of slice 2 on the committed build `8d8c58574`: `E:/Temp/scenario-runs/20261121-201346` (the defence, lost in wave 2)
and `20261121-202155` (the hunt, won). `python tools/ingame_verdict.py --issue 1105 --session <log>` reads PASS for the four rows of
each mission.

Slice 3, by hand until the offer has a driver: in a single-player campaign in the Isengard War or later, run
`taom.chronicle_open breaking confirm`. The popup appears on the free map. Choose "Go to Parth Galen", then a side, play the
mission and choose "Continue". The line `taom.story-missions.offer.result` ends `result=Averted hero=lord_1_75` or
`result=Occurred hero=lord_1_75`, and the `[WarChronicle]` line of the event names what the kill did.

The free heroes (#1366). A driver for the story mission exists since #1368 (step 0 below), but it has not run yet, so a person
still runs this by hand until it has, in a loaded campaign on a build that holds the feature (a `.dirty` build is not counted).

0. The unattended route: `python tools/run_scenario.py --kind story-mission --mission helms_deep_defend` starts a new campaign,
   opens the mission through the dev menu's click path (`Open the mission`), waits for the end and chooses `Back to the map`, then
   quits. It reaches the mission rows of `1366.toml` for the one defence mission, and `python tools/ingame_verdict.py --issue 1368` reads
   its own three rows. It does not reach: step 5 (a person's judgement), the War Chronicle's offer path, a save and reload, a forced
   loss, or any other mission id. The driver is unit tested only; its first run is owed.


1. Start a new campaign or load a save, single player, on the map with no menu open. The line
   `[Check] id=taom.story-missions.roster result=pass` appears at session start, with `missing=-` and
   `items_missing=-`. This step needs no person: `python tools/run_scenario.py --days 2` on a build
   that holds this change starts a new campaign unattended and writes the line.
2. Run `taom.story_open helms_deep_defend auto`, then choose "Open the mission".
3. Play or let the bot play one wave to its clear, then let the mission end.
4. Run `python tools/ingame_verdict.py --issue 1366`. It reads the roster line, three
   `taom.story-missions.hosted.hero` passes (one per hero, `is_hero=0 campaign=1`), the `hunters`
   line, each hero's `fired` line, Legolas's `precision` and `multishot` (`item=1`), and the aura line.
   A hero who casts nothing in a short run reads NOT REACHED, not a fail: read the log before calling
   it a defect. Each mission row reads from the story launch line to the next `[LongDefeat]` or
   `[StoryMissions] launching` line (the manifest's `scope` and `scope_end`). A campaign battle fought
   after the story mission writes no launch line and is still read, and the aura row can then match the
   companion Aragorn: read the verdict from a game process that fought no other battle after the story
   mission. Every row of `1359.toml` and `1361.toml` reads only a Long Defeat run, so this run does
   not count for them.
5. The rows a person judges are in the manifest: key 8 selects the three and they hold the nearest wall
   lane; after the player falls he can take control of a hero, who keeps his health and casts nothing
   while controlled; the War Chronicle's offer opens the same mission; a new save and an old save each load,
   play a day, save under a new name and reload clean; no `taom_hero_*` shows in the encyclopedia, the
   party screen or the companion pool; the campaign is unchanged after the return; the three look as
   they do in the Long Defeat, and at the lowest Character Detail setting each has his own face; a save
   made on this build loads on a trunk build.

What one run has proven: the unattended run `E:/Temp/scenario-runs/20261116-035100`
(`python tools/run_scenario.py --days 2`, on trunk 317413337 plus only the two `SubModule.xml` nodes)
started a new campaign with the hero characters and the extra arrow loaded and ran 2 of 2 days with a
clean unload. The engine log of that run shows both files opened. It did not prove a save, a load,
the roster line (not built then) or the story mission.

What two more runs have proven, both on the committed build 8ee4ef2fe. The unattended run
`E:/Temp/scenario-runs/20261116-055126` (`python tools/run_scenario.py --days 2`) started a new campaign
and wrote `[Check] id=taom.story-missions.roster result=pass missions=7 troops=34 missing=- items=1
items_missing=-`: every troop of the seven story missions, the three heroes among them, and the extra
arrow resolve at session start. `python tools/ingame_verdict.py --issue 1366` reads PASS for the roster row
and NOT REACHED for the mission rows. The unattended Long Defeat run `E:/Temp/long-defeat-runs/20261116-055255`
(3 waves) is the check that the changed hero spawn and the level did not break the custom game: the three
hero lines read `is_hero=1 campaign=0 ... health_factor=3.00` with their own weapons, and
`python tools/ingame_verdict.py` reads PASS for #1337 and for all seven rows of #1361.

The story mission itself is proven by the unattended run `E:/Temp/scenario-runs/20261116-075342` (the driver of #1368,
build 4baa2f53a): `python tools/ingame_verdict.py --issue 1366` reads PASS for all seven rows. The three hero lines read
`is_hero=0 campaign=1 mounted=0 formation=7 held=1 strangers=0 order=Move health_factor=3.00` with `strider_sword`,
`wm_mirkwood_bow_a01` and `sm_dwarf_dain_axe_a` (the kit of that run; since #1373 Gimli carries `sm_dwarf_erebor_axe_2h_c`, and `1366.toml` accepts both). The hunters line passed at the wave clear with all three standing and in place.
Each cast (`fired=1`, `8` and `2`), Legolas's aim read zero, Many Arrows fired ten extra arrows with the campaign-loaded item
(`item=1`), and Aragorn's aura reached 102 soldiers, which in a campaign only the new source rule can do. The mission was won
(108 kills, 9 defenders lost).

One number of that run is not healthy: the hunters line ends `retaken=1 moved_out=1517`. A second run with one temporary
log line (`E:/Temp/scenario-runs/20261116-081024`, `moved_out=1481`) shows that about once a second during the preparation
before the first wave (24 repairs in 21 seconds) the whole Infantry formation is moved into the heroes' formation and out
again by the repair (20, then 40, 60, 80, 100 soldiers), and that the last repair comes in the second the wave starts, with none after it. It never shows in a
Long Defeat (`moved_out=0` in every run there). The checks passed because the repair won each time. This is #1370; the fix
is below, and its proof run is owed.

What is measured: the mover is the engine's castle tactic. Before the attackers exist no lane is active, so it defends only the
lanes that have a gate, which is one lane (`TacticDefendCastle.cs:1005-1016`). It then has two melee formations for one lane:
the garrison's Infantry formation, under AI control, and the heroes' formation, off AI control by the design of #1337.
`ConsolidateFormations` ranks a formation that is not AI controlled first as the target (`TacticComponent.cs:191`) and
transfers every AI-controlled formation into it (`:208`). When the wave starts, more lanes are active than there are melee
formations, and the consolidation stops.

Why the Long Defeat never shows it: the consolidation runs only while no lane holds a stored defender formation
(`SiegeLane.GetLastAssignedFormation`; with one, the tactic skips a formation off AI control, `TacticDefendCastle.cs:590`).
The tactic makes its first pick in the frame the garrison spawns. A Long Defeat hero is bought, so he spawns one frame later
at the earliest, after that pick. The story mission's free heroes were spawned in the same frame as the garrison, before it.
A probe run proved it (`E:/Temp/scenario-runs/20261116-125931`): with the heroes held until the first pick, the Middle lane
kept formation 0, there were 0 repairs and `moved_out=0`.

The fix: the director holds every hero the run asks for until a lane holds a defender formation, or for at most 5 s, then
spawns him with the warning `[Hero] no wall lane holds a defender formation after 5 s; the heroes spawn now`
(`LongDefeatDirector.Heroes.cs`, `ILongDefeatBattlefieldAdapter.DefenderLanesAssigned`). It guards the first pick only. A
lane that becomes active later is picked from every formation with units, off AI control or not
(`TacticDefendCastle.cs:481-493, 701-735`): in the probe run the Right lane took formation 7 about 21 s in, and the run still
read `moved_out=0`. How often a later pick moves soldiers is not known; `tools/ingame_checks/1370.toml` reads `moved_out=0`
on the hunters line, so a run measures it.

Not proven: that an XML character no party holds survives a save and reload, and every row a person must look at (the order
key, the take-over, the faces, the War Chronicle offer path).

## How to add a story mission

1. Add a row to `story_missions.json` with a Defend side, two culture ids, a garrison and at least
   one wave. A row with a second wave also gets The Long Defeat's engine restore (#1160): a ram or
   siege tower destroyed in one wave stands again at the start of the next. Every row today has one
   wave, so no story mission meets it yet.
2. Check every troop id and culture id with `python tools/validate_moduledata.py`.
3. Run `taom.story_open <id>` once to time the scene load. A load of many minutes needs a stand-in.
4. No code changes are needed.

## Not built yet

Slice 5 is built as a mission (#1409) and no window offers it. The Hornburg and the Breaking of the
Fellowship are the two events that offer a mission. No unattended driver reaches the offer path yet
(the popup, the menu and the result), so slices 3 and 4 have no machine proof of it. Slice 4 was built before slices 2 and 3, because a
player report showed the Hornburg ending with no battle (2026-11-13).

## Known limits

- In an Attack row (#1409) the player's body stands on the attacker team, and some Long Defeat code still treats it as a wave soldier or
  the defender team as the player's. The hooks and effect sampling keyed by side (`LongDefeatStatHooks`, `LongDefeatCombatHooks`,
  `LongDefeatEffectsAdapter`, morale, the gate watch) do not skip the main agent. The sally helpers do the same: `RemountPlayer` takes
  `DefenderTeam`, and `NearestAttackerTo` includes the main agent. No story plan reaches this code today, because a story plan has no
  affix, scaling, boon, shop or mounted class. It becomes live if a story attack gets one of them; fix it then.

## Changelog

- 2026-11-21: Slice 3, the Breaking of the Fellowship end to end: a Narrative event's hero story, its window of 10 days, the
  state `Averted`, the kill by `ApplyByBattle`, and the offer's two new options. Unit tested; no game run yet.
- 2026-11-21: Slice 2, the Parth Galen field missions (`parth_galen_save`, `parth_galen_kill`): the field list and start places,
  the hero who must live, Boromir's mission character, two attacker start places, groups under a live cap, and heroes under the
  team AI on a field. Proven by unattended runs; no person has played them.
- 2026-11-16: #1373, Gimli's kit is now the Erebor two-handed axe `sm_dwarf_erebor_axe_2h_c`; the hunters line gains `weapons=`, and `tools/ingame_checks/1373.toml` reads both. Unplayed.
- 2026-11-16: #1370, the churn of the garrison's foot through the heroes' formation before the first wave is measured and
  checked by `tools/ingame_checks/1370.toml` (red on the bug). Fixed: the heroes wait for the castle tactic's first lane
  pick, at most 5 s. Proof run owed.
- 2026-11-16: #1366, Aragorn, Legolas and Gimli stand free with the garrison in the Helm's Deep
  defence of a campaign; the roster check at session start. After review: the heroes keep their own
  faces and, when taken over, their own health; the verdict tool's `scope_end` keeps a later Long
  Defeat run out of the #1366 rows and the story mission out of the #1359 and #1361 rows. Not yet
  opened in a game.
- 2026-11-15: #1326, the offer follows the player's side of the war (defend, attack or no offer),
  and a story block may name an `attackMission`. No attack mission ships yet.
- 2026-11-13: Slice 4, the Hornburg's offer of the Helm's Deep defence and its campaign result.
- 2026-11-12: this doc, from the code on trunk.
- 2026-11-10: Slice 1 (the campaign host and `taom.story_open`), the mission's own music, and seven
  Defend rows; four rows on stand-in scenes after one-wave probes. Reviews:
  [slice 1 RCA](../reviews/rca-story-missions-slice1-2026-11-10.md),
  [music RCA](../reviews/rca-story-mission-music-2026-11-10.md). Codex pre-review waived by Mike on
  2026-11-11.

## GitHub Issue

- **Issue:** #1105
- **Issue:** #1366, Phase 6 of #1325: the free heroes (built, in-game proof owed)
- **Status:** Open (Slices 1 to 4 built; slice 5 built as a mission under #1409; a person's play owed for all)
