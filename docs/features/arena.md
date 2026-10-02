# Arena (Tournament Model + Dwarf Dismount + Exit-Hang Fix + Winner-Panel Guard)

## Overview

Replaces vanilla's tournament model with a culture-aware, race-aware variant. Six concerns:

1. **Per-participant culture armor** — participants wear armor matching their *own* culture (a dwarf gets dwarf gear, not the host town's human kit), preventing skeleton-clipping. Data-driven via `gear_practice_dummy_<culture>` NPCs — see [tournament-armor-assignment.md](tournament-armor-assignment.md).
2. **Class-capped, culture-filtered prize pools:** every prize is light, medium or heavy on the armour ladder and below; a small tournament awards light and medium, a big one heavy, drawn from the host town's culture. Troll gear and elite, lord and named kit are never prizes. See [Prize pools](#prize-pools).
3. **Dwarf dismount (Patch46, 2026-06-09)** — dwarves never fight mounted in tournaments. Their custom (shorter) skeleton clips them *inside* the horse mesh. A Harmony postfix strips the horse from dwarf participants.
4. **Tournament-exit hang fix (Patch60 + PatchShield exclusion, 2026-07-06→10, #331)** — exiting any tournament froze the exit 30s–2min (measured 104–109s, three times). **Round 1 (Patch60):** engine defect — `MissionGauntletTournamentView.OnMissionScreenFinalize` nulls its `_gauntletMovie`/`_gauntletLayer` **without releasing them** (the practice view releases correctly). [Patch60_TournamentExitMovieRelease](../../Main/Features/Arena/Hooks/Patch60_TournamentExitMovieRelease.cs) captures the layer/movie in a Prefix (the original body nulls the fields) and, in a Postfix — after the body has dropped focus + finalized the VM, so `TryLoseFocus` can't NRE — replicates the practice view's `ReleaseMovie` → `RemoveLayer` sequence at `OnEndMission` time. Fail-safe → vanilla leak; drift-guard tests pin the bindings; its per-exit `ReleaseMovie=Nms` log line is the permanent regression canary. **Necessary but not sufficient:** the ~107s moved WITH the relocated release, proving the release itself was the sink. **Round 2 (the real fix):** the `ExitStallSampler` stack-sampled the frozen main thread and named a three-factor interaction — the tournament UI's per-round template re-instantiation accumulates `WidgetTemplate._customTypeChildren` into a ~10^6-call release recursion; UIExtenderEx legitimately patches `WidgetFactory.IsCustomType`/`WidgetTemplate.OnRelease`; and TAOM.Dependencies' **PatchShield** stacked a `__originalMethod`-binding finalizer on every patched method, adding ~50µs of reflection per call. Fix: `PatchShield.ExcludedTargetNamespacePrefixes` (now `PatchShieldPolicy.ExcludedTargetNamespacePrefixes`, plan 007) never shields `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension`. **Measured: 105–109s → 9.5s** (residual = UIExtenderEx's legitimate wrapper at ~10^6 calls; accepted). Full chain: [rca-tournament-exit-hang-2026-07-06.md](../reviews/rca-tournament-exit-hang-2026-07-06.md) round-2 section.
5. **Exit AV containment (Patch62, 2026-07-13, #339)** — a v2.0.12 player CTD'd exiting a won tournament: heap-corruption `AccessViolationException` inside `WidgetFactory.IsCustomType` → `Dictionary.FindEntry` during the Tournament movie's `WidgetTemplate.OnRelease` walk (corrupt template-tree string; prize tableau render in flight at exit). Patch60's fail-safe caught the first AV, but "fall back to the vanilla leak" meant `GauntletLayer.ClearContext` re-walked the same corrupt tree at `ScreenManager.PopScreen`, uncaught. [GauntletMovie_Release_AvGuard_Patch](../../Main/Features/Arena/Hooks/GauntletMovie_Release_AvGuard_Patch.cs) is an AV-only Finalizer on `GauntletMovie.Release` — the shared chokepoint both attempts flow through — converting the crash into one logged leaked movie; suppression on the first attempt also removes the movie from the layer so the re-walk never happens. Root cause is engine/native territory; this is containment. Registry: [harmony-patch-registry.md](../reference/harmony-patch-registry.md) § Patch62.

6. **Winner-panel null guard (Patch69, 2026-08-07, #407)** — vanilla `TournamentVM.OnTournamentEnd` sets the winner's armour colours via `hero.MapFaction.Color` (hero branch) or `character.Culture.Color` (troop branch), neither guarded. `Hero.MapFaction` genuinely returns null for a clanless, non-special hero with no home settlement and no party, so such a winner NREs the panel — reported as a crash after "Skip All Rounds" at Erebor (bundle `d7d9f7d3`, v2.0.18.0). [Patch69_TournamentRosterGuard](../../Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs) **substitutes** offending entrants with the culture's elite/basic troop at `GetParticipantCharacters`. It must never *remove* one: vanilla pads the roster to exactly 16 and `TournamentMatch.AddParticipant` reads `participant.Team` unguarded, so a short roster crashes on entry instead — see the registry entry for the full chain. [Patch69_TournamentEndGuard](../../Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs) is a finalizer that dumps the bracket and swallows, covering the two further null sites we could not reproduce (`TournamentParticipantVM.Refresh(null, …)` nulls `Participant` but never clears `IsValid`).

Tournament start/end timing constants are also exposed for tuning.

## A tournament crash is not automatically a tournament bug

The `d7d9f7d3` triage is worth remembering as a shape. The reporter's stated theory was "female
dwarves crash my game", and the artifact they sent was a tournament crash from a dwarf campaign — two
different bugs that looked like one:

- The **tournament** crash was a managed NRE in a vanilla view model, unrelated to sex or race, and is
  what Patch69 addresses.
- The **female dwarf** crashes were a native access violation from a single unresolved mesh name in
  `LOTRLOME_Armory/ModuleData/skins.xml` (`sk_dwarf_underwear_female` vs the shipped
  `sk_dwarf_underwear_female_a`) — no managed exception, therefore no crash bundle at all. Fixed
  separately (#403); gate: `python tools/validate_mesh_refs.py --no-rgl-log`. **Corrected 2026-09-26:** the
  crash's faulting address (`0x24C`, 98 x 6) decodes to the static face morph over `sk_dwarf_bm_f1_head.eye`, a
  face part with no morph channels (#385); the underwear name was a second defect. The female meshes are restored
  and she no longer crashes ([race-face-and-hand-morphs.md](../reference/race-face-and-hand-morphs.md)).

The data sweep that preceded the fix is worth not repeating: every `<NPCCharacter>` in the load order
(5,166 across TAOM, TAOM_Map, SandBoxCore, SandBox, Native, LOTRLOME_Armory) carries a `culture=`
attribute except two multiplayer-only entries; `as_dwarf_*` action sets are at 90/90 parity with
`as_human_*`; and all 13 TAOM female dwarf lords carry a `faction=` in `heroes.xml`, so their
`MapFaction` is non-null. None of those was the cause. **A reporter's theory names the symptom they
noticed, not the defect** — and one bundle does not necessarily describe the crash they were
complaining about.

## The arena crowd is not TAOM's, and it is not a bug

Every agent in a tournament with a townsfolk id — `musician_dunland`, `townsman_dunland`,
`armorer_dunland`, `merchant_dunland`, `ransom_broker_dunland` — is a **spectator**, spawned by the
engine's `MissionAudienceHandler` (`SandBox.View`) from a weighted draw over the settlement culture's
location characters:

```
Townswoman 0.2 · Townsman 0.2 · Armorer 0.1 · Merchant 0.1 · Musician 0.1
Weaponsmith 0.1 · RansomBroker 0.1 · Barber 0.05 · FemaleDancer 0.05
```

They arrive early — `EarlyStart` → `OnInit` → `SpawnAudienceAgents` → `Mission.SpawnAgent` — so they
occupy the **first agent indices** of the load, ahead of any tournament participant. A crash log
whose last line names `agent#0` in an arena is naming a spectator, not a fighter.

This is worth stating because the opposite conclusion is easy to reach and was reached, three times
independently, during the 2026-08-02 Dunland tournament CTD: `OpenTournamentFightMission` returns 13
behaviors with no `MissionAgentHandler`, so a townsfolk agent looks impossible — until you notice
`MissionView`s are registered separately and the live mission holds **65** behaviors. `#295`
(arena-stand spectators rendering naked) was the same population seen from the art side.

Read the live list from `[MissionDiag] === Mission start: … behaviors=N ===`, never the initializer
delegate. Investigation: [investigation-dunland-tournament-ctd-2026-08-02.md](../reviews/investigation-dunland-tournament-ctd-2026-08-02.md).

## Why This Exists

- **Vanilla behavior:** [DefaultTournamentModel](E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\GameComponents\DefaultTournamentModel.cs) returns participant-agnostic armor — every participant in a town tournament wears the host culture's kit regardless of their own. Reward items come from a global pool with no cultural filtering. And the tournament *weapon* templates (`CultureObject.TournamentTeamTemplatesFor{One,Two,Four}Participant`, or the `tournament_template_empire_*` fallback) include mounted loadouts, so some participants are spawned on horseback.
- **TAOM requirement:** TAOM ships per-race skeletons (dwarves, elves, hobbits) via [hero-race.md](hero-race.md). (a) Human armor on a dwarf skeleton clips through the body. (b) A **mounted** dwarf is worse: the dwarf's custom rider bone is misaligned, so the dwarf model spawns *inside* the horse — the same defect the `EyeHeightAdjustmentHook` eye-height workaround exists for. Each culture also has LOTR-themed weapon/armor families that tournament rewards should reflect.
- **Without this feature:** armor clipping during tournaments + thematic wrong-faction reward items + **dwarves visibly stuck inside horses** when their loadout rolls a mounted template.

## Architecture

> **Architecture note (Phase 9b #137):** all decision logic was extracted from the GameModel into [`TournamentService`](../../Main/Features/Arena/TournamentService.cs) to satisfy the rule-4 "no inline branching in GameModel overrides" constraint ([.claude/rules/gamemodels.md](../../.claude/rules/gamemodels.md)). [`TaomTournamentModel`](../../Main/Features/Arena/Models/TaomTournamentModel.cs) is now a **thin entry point** that converts sealed TaleWorlds params to primitives at the boundary and delegates to the injected `ITournamentService`. Earlier revisions of this doc described logic living on the model — that is no longer accurate.

### Design Challenge

`DefaultTournamentModel.GetParticipantArmor` takes a `CharacterObject` but returns a single `Equipment` built from the host town's culture — no per-participant resolution. **The mount is a separate problem entirely:** `GetParticipantArmor` only governs armor/clothing (slots 5–9). The horse (slot 10) comes from a *different* path — `TournamentFightMissionController.PrepareForMatch` clones the culture weapon template into each `participant.MatchEquipment`, and `AddRandomClothes` (which calls `GetParticipantArmor`) copies only slots 5–9 on top. So overriding `GetParticipantArmor` can never remove a horse — that required a separate Harmony patch (Patch46).

### Solution Approach

Two extension points, both delegating to `ITournamentService`:

**(A) GameModel override** — [TaomTournamentModel](../../Main/Features/Arena/Models/TaomTournamentModel.cs) inherits `DefaultTournamentModel` and overrides five methods:

| Override | Delegates to | Behavior |
|---|---|---|
| `GetParticipantArmor(CharacterObject)` | `ResolveDummyId` | Resolves a `gear_practice_dummy_<culture>` NPC by the participant's culture, returns its `RandomBattleEquipment`; falls through to base if not found. |
| `GetRegularRewardItems(Town, …)` | `BuildPrizePool(culture, PrizeBand.Regular)` | Light, medium and civilian class (Tierf 2 and up) from `Items.All`, weapons and armour only, no horses: the town's culture, else every culture's. Base only if both are empty. See [Prize pools](#prize-pools). |
| `GetEliteRewardItems(Town, …)` | `BuildPrizePool(culture, PrizeBand.Elite)` | Same builder, heavy class only. |
| `GetTournamentStartChance(Town)` | `CalculateStartChance` | Boundary computes lord count; service maps it: 0→0%, 1→45%, 2→75%, 3→90%, 4+→100%. Returns 0% if the town is under siege or outside a campaign. |
| `GetTournamentEndChance(TournamentGame)` | `CalculateEndChance` | After a 20-day grace period, ramps end-chance by 3.3%/day elapsed. |

**(B) Harmony postfix (Patch46_TournamentDwarfDismount)** — [Patch46_TournamentDwarfDismount](../../Main/Features/Arena/Hooks/Patch46_TournamentDwarfDismount.cs) postfixes the public `TournamentFightMissionController.PrepareForMatch`. After vanilla assigns every participant's `MatchEquipment`, it iterates all teams/participants and, for any participant whose race `ShouldDismountInTournament` returns true (currently dwarves), clears `EquipmentIndex.Horse` + `EquipmentIndex.HorseHarness` via `AddEquipmentToSlotWithoutAgent(slot, EquipmentElement.Invalid)`. `PrepareForMatch` is the single chokepoint feeding **both** the visual spawn (`SpawnAgentWithRandomItems`) and the AI simulation (`Simulate` → `GetSimulationAttackPower`), so a dwarf is never treated as cavalry anywhere in the tournament. Keyed on **race, not culture**, so a dwarf competing in *any* town — and the player, if the player is a dwarf — is caught.

`ShouldDismountInTournament(int raceId)` uses the **validate-before-lookup** pattern ([.claude/rules/csharp-architecture.md](../../.claude/rules/csharp-architecture.md#lookup-functions-with-fallbacks-validate-before-lookup)): `IRaceManager.GetRaceNameFromId` returns `"human"` as a fallback for unknown ids, so the service guards with `IsValidRaceId` *before* the name lookup, then compares to `"dwarf"` case-insensitively. It uses the same `IRaceManager` as [`EyeHeightAdjustmentHook`](../../Main/Features/HeroRace/EyeHeightAdjustmentHook.cs) for the `dwarf` check, but additionally guards with `IsValidRaceId` before the lookup (which that hook does not).

### Component Diagram

```
== (A) GameModel ==
SubModule.OnGameStart  (Main/SubModule.cs:385)
   campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<ITournamentService>()))
        |
TaomTournamentModel : DefaultTournamentModel        ← thin: converts sealed→primitive, delegates
        |                                               (logic lives in TournamentService, #137)
   GetParticipantArmor / GetRegularRewardItems / GetEliteRewardItems
   GetTournamentStartChance / GetTournamentEndChance
        |
   ITournamentService (TournamentService, Reuse.Singleton, injects IRaceManager + IArmourGateService)
        ├─ ResolveDummyId(cultureId)        → "gear_practice_dummy_<culture>"
        ├─ BuildPrizePool(culture, band)    → filter Items.All by TournamentPrizeRules
        ├─ CalculateStartChance / CalculateEndChance
        └─ ShouldDismountInTournament(raceId) → IRaceManager validate + "dwarf" check

== (B) Harmony postfix (Patch46) ==
TournamentFightMissionController.PrepareForMatch()   ← vanilla assigns MatchEquipment (incl. horse)
        | [Postfix]
Patch46_TournamentDwarfDismount.Postfix(____match)   // 4 underscores: ___ prefix + field _match
   foreach team → foreach participant:
      if service.ShouldDismountInTournament(participant.Character.Race):
         clear MatchEquipment[Horse] + [HorseHarness] = EquipmentElement.Invalid
        |
SpawnAgentWithRandomItems / Simulate read the now-horse-free MatchEquipment → dwarf on foot
```

## Configuration

None. All knobs are constants on `TournamentService` / `TaomTournamentModel`. The dwarf-dismount race set is a one-line extension point (`DwarfRaceName` constant) — intentionally not config (per the simplicity criterion; spiders/other non-humanoids are recruitable troops, not tournament heroes — see memory `nonhumanoid-creature-troop-not-mount`).

| Constant | Location | Value | Meaning |
|---|---|---|---|
| `RegularMinTierf` | `TournamentPrizeRules` | `2f` | Junk floor of the regular prize band; the bands themselves are armour classes |
| `TournamentStartChance1Lord` / `2Lords` / `3Lords` | `TournamentService` | `0.45f` / `0.75f` / `0.90f` | Start probability by lord count |
| `TournamentEndChanceGraceDays` | `TournamentService` | `20f` | Grace before end-chance ramps |
| `TournamentEndChanceRamp` | `TournamentService` | `0.033f` | Per-day end-chance increment after grace |
| `DwarfRaceName` | `TournamentService` | `"dwarf"` | Race name that forces dismount in tournaments |

To change armor or rewards, **edit XML, not code** — add/edit `gear_practice_dummy_<culture>` NPCs in `Main/_Module/ModuleData/characters/npcs_<culture>.xml`.

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/Arena/Models/TaomTournamentModel.cs](../../Main/Features/Arena/Models/TaomTournamentModel.cs) | GameModel override (thin) — 5 overrides, each delegates to `ITournamentService` |
| [Main/Features/Arena/ITournamentService.cs](../../Main/Features/Arena/ITournamentService.cs) | Service interface — `CalculateStartChance` / `CalculateEndChance` / `BuildPrizePool` / `ResolveDummyId` / `ShouldDismountInTournament` |
| [Main/Features/Arena/TournamentService.cs](../../Main/Features/Arena/TournamentService.cs) | Service impl (`Reuse.Singleton`); injects `IRaceManager` for the dwarf check and `IArmourGateService` for prize classes |
| [Main/Features/Arena/TournamentPrizeRules.cs](../../Main/Features/Arena/TournamentPrizeRules.cs) | Pure prize rules: `PrizeBand`, `PrizeClass`, `Fits` |
| [Main/Features/Arena/Hooks/Patch46_TournamentDwarfDismount.cs](../../Main/Features/Arena/Hooks/Patch46_TournamentDwarfDismount.cs) | Harmony postfix — clears Horse/HorseHarness for dwarf participants |
| [Main/Features/Arena/ArenaIoC.cs](../../Main/Features/Arena/ArenaIoC.cs) | `container.Register<ITournamentService, TournamentService>(Reuse.Singleton)` |
| [Main/SubModule.cs:385](../../Main/SubModule.cs) | `AddModel(new TaomTournamentModel(IoC.Resolve<ITournamentService>()))` |
| [Main/SubModule.cs](../../Main/SubModule.cs) | `_harmony.PatchCategory("Patch46_TournamentDwarfDismount")` (next to Patch45_SpiderTroopSpawn) |
| `Main/_Module/ModuleData/characters/npcs_<culture>.xml` | `gear_practice_dummy_<culture>` NPCs (armor data) — see [tournament-armor-assignment.md](tournament-armor-assignment.md) |

## Dependencies

- `TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel` (base class)
- `SandBox.Tournaments.MissionLogics.TournamentFightMissionController` (Patch46 target — SandBox.dll; private field `_match` injected as `____match` — **four** underscores: Harmony's `___` prefix + the field name `_match`. Using three (`___match`) crashed the game on load; see RCA 2026-06-09.)
- `TaleWorlds.CampaignSystem.TournamentGames.{TournamentMatch, TournamentTeam, TournamentParticipant}` (iterated in Patch46)
- `TaleWorlds.Core.{Equipment, EquipmentIndex, EquipmentElement}` (slot clearing)
- [`IRaceManager`](../../Main/Core/Domain/IRaceManager.cs) (TAOM, `Reuse.Singleton`) — race-id → race-name resolution for the dwarf check
- `Game.Current.ObjectManager` (resolves `gear_practice_dummy_<culture>` NPCs) + `Items.All` (prize-pool filtering)

## Tests

- [TAOM.Tests/Features/Arena/TournamentServiceTests.cs](../../TAOM.Tests/Features/Arena/TournamentServiceTests.cs) — **21 tests**: start-chance step function, end-chance ramp, `ResolveDummyId` fallback chain, and **6 for `ShouldDismountInTournament`** (dwarf→true, mixed-case "Dwarf"→true, human/elf/orc→false, invalid race id→false with `DidNotReceive().GetRaceNameFromId` asserting validate-before-lookup). `IRaceManager` is mocked via NSubstitute.
- [TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs](../../TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs): the tuning constants' invariants.
- [TAOM.Tests/Features/Arena/TournamentPrizeRulesTests.cs](../../TAOM.Tests/Features/Arena/TournamentPrizeRulesTests.cs): which class, tier and merchandise flag each band accepts, weapons by engine tier included.

The `Patch46` postfix and the model methods that touch `Game.Current.ObjectManager` / `Items.All` are game-only (not unit-tested per ADR-008). The testable decision logic lives in `TournamentService` and `TournamentPrizeRules`; `BuildPrizePool`'s loop over `Items.All` is game-only.

## Prize pools

The engine picks a prize in `FightTournamentGame.GetTournamentPrize` (v1.5.3, not virtual): fewer than
4 hero entrants draw from `GetRegularRewardItems` by value quartile (the quartile is the hero count),
4 or more from `GetEliteRewardItems` (the cheaper half below 10 heroes, the dearer half from 10). TAOM
supplies both lists through [TournamentPrizeRules](../../Main/Features/Arena/TournamentPrizeRules.cs)
(Mike, 2026-10-02: every prize is heavy or below):

| Band | Accepts | Never |
|---|---|---|
| Regular | light, medium or civilian class, Tierf 2 and up | heavy and above |
| Elite | heavy class | light, medium, and anything above heavy |
| Both | weapons and armour of the band, from the town's culture, else every culture's | horses, elite, lord, named kit, anything `is_merchandise="false"` in its XML |

- **Class** is the armour table's class ([armour-acquisition.md](armour-acquisition.md)) through
  `IArmourGateService.GetClass`. A weapon, shield or harness has none, so it takes the class its engine
  tier implies (`ArmourClassRules.FromEngineTier`): Tier3 is medium, Tier4 heavy, Tier5 and Tier6
  elite and never a prize.
- **Merchandise** is the XML value from before the armour gate flips heavy and above to
  `NotMerchandise`: the gate's record keeps it (`GetRecord(id).IsMerchandise`). That is how a gated
  heavy piece can still be a prize while the troll gear, which is `is_merchandise="false"` in the
  Armory (`CREATURE_GEAR_OBTAINABLE` gates it), never is. Without a record (a failed gate init) the
  live flag decides.
- **The list must never be empty.** The engine indexes it unguarded
  (`cache[MBRandom.RandomInt(min, max)]`, `FightTournamentGame.cs:365` and `:392`), so an empty list
  crashes the roll. The service falls back from the town's culture to every culture's items; the
  model's `base` call after that is a last resort no real item set reaches.
- **Old saves** pick the rules up without help: the candidate lists are not saved, and the join menu
  re-rolls the prize (`UpdateTournamentPrize(includePlayer: true)` changes the hero count).

## Extension points (engine facts, v1.5.3)

What a future tournament feature can hook, read from the decompile on 2026-10-02:

| Want | Where | How |
|---|---|---|
| A higher max bet | `TournamentBehavior.GetMaximumBet()` (SandBox): `150`, doubled by Roguery's Deep Pockets. The bet UI, the bet button and its text all read it (`TournamentVM.cs:207`, `:922`, `:1066`) | **Done** (Patch96, [tournament-rewards.md](tournament-rewards.md)) |
| Renown and influence for the win | `TournamentModel.GetRenownReward` (vanilla 3, Duelist doubles, Self Promoter +3) and `GetInfluenceReward` (vanilla 0, an `int`) | **Done** on `TaomTournamentModel` ([tournament-rewards.md](tournament-rewards.md)) |
| The skill a tournament trains | `GetSkillXpGainFromTournament` (vanilla 500 XP to a random skill) is called only for off-screen tournaments (`TournamentManager.SimulateTournament`), never for one the player plays | **Done** as a choice at Join, paid on `TournamentFinished` and `PlayerEliminatedFromTournament` ([tournament-rewards.md](tournament-rewards.md)) |
| How often each culture holds one | `GetTournamentStartChance` | `TournamentService.CalculateStartChance`. Declined (Mike, 2026-10-02): tournaments matter to player development everywhere |
| A refund when knocked out | `TournamentBehavior` pays the stake to the town (`:280-283`) | Declined (Mike, 2026-10-02) |
| Choosing the prize | `GetTournamentPrize` is abstract on `TournamentGame` and overridden by `FightTournamentGame`, but only the automatic roll; `TournamentGame.Prize` has a private setter | **Done** as a choice of three at Join, written through the private setter ([tournament-rewards.md](tournament-rewards.md)) |
| 16 entrants, teams of 4, 15-day life and cooldown | `FightTournamentGame.cs:37-43`, `TournamentCampaignBehavior.cs:17` | Harmony patches |

## Parked: archery contests and jousting

Parked by Mike on 2026-10-02: both need arena scenes built for them first. What the research found, so the work
can restart from here:

- **The engine already ships both mission types.** `SandBoxMissionManager` exposes `OpenTournamentArcheryMission`
  and `OpenTournamentJoustingMission` beside the fight one (v1.5.3 `SandBox.SandBoxMissionManager.cs:22-29`), and
  `TournamentMissionStarter` builds each mission's behaviors. Nothing on the campaign side calls them:
  `FightTournamentGame.OpenMission` calls only the fight mission (`FightTournamentGame.cs:102`). A third type,
  the horse race, is a stub whose match methods throw `NotImplementedException`.
- **The bracket, betting, rewards and UI are generic.** `TournamentBehavior` drives any game behavior through the
  four-method `ITournamentGameBehavior`, and `TournamentVM` reads only `TournamentBehavior`, so Patch69's guards
  and this feature's rewards would apply unchanged.
- **Archery** (`TournamentArcheryMissionController`): scores by destroying `DestructableComponent` entities tagged
  `archery_target` (the stock `archery_target_pot` prefab, `MaxHitPoint` 0.1). It accepts the fight game's team
  shape, so the lightest version is a redirect of `FightTournamentGame.OpenMission` for flagged towns, with no new
  saved type. Its gear is hardcoded vanilla Calradian items (bow, blunt arrows, armour) and would need a patch to
  look Middle-earth. Its `OnAgentHit` ends the whole mission on any agent hit: check that before shipping.
  Vanilla's Khuzait and Vlandian arenas carry working targets; TAOM's `taom_gondor_arena_001/002` scenes carry a
  full archery layout but no settlement uses them (ask Mike whether that is intended).
- **Jousting** (`TournamentJoustingMissionController`): one-on-one lance passes, first to three, a sword duel if
  both are unhorsed, AI lanes driven by scripted positions. It needs a game with team size 1 and two teams per
  match (a dedicated `TournamentGame` subclass, a new saved type with its own `SaveableTypeDefiner` base), and a
  participant filter that excludes races that cannot ride (dwarves, Patch46). **No vanilla or TAOM scene carries
  the jousting entities** (`sp_jousting_*`, `region_box_*`, `region_end_box_*`, `jousting_barrier`), so every arena
  that should host it needs a tilt lane built in the editor.

## An arena character with no `<face>` fights as a toddler

Players reported the arena's "Practice Fighter" and "Gear Dummy" spawning waist-high next to a normal
troop. The cause was entirely in the data, and it is worth knowing because nothing anywhere reports it.

`BasicCharacterObject.Deserialize` (v1.4.8) declares two local `BodyProperties` initialised to
`default`, fills them from the `<face>` node, and then, if no `face_key_template` was read, registers
the character's `MBBodyProperty` from those two locals. With no `<face>` element at all they stay
all-zero, so the character's body-properties age is 0, and `skins.xml` maps age 0 to
`mesh_maturity_type="toddler"`: `min_scale` 0.52 against the adult 1.07. Every race in the merged
`skins.xml` has a toddler skin, so this is not a human-only failure.

The two guards in `Mission.SpawnAgent` both miss it, because they read a different age. That method
takes `agentCharacter.Age`, forces 29 when it is exactly 0, and forces 27 for a sub-teenager in a
battle-like mission mode. But `Age` is a separate property, clamped at deserialisation to
`max(20, BodyPropertyMax.Age)`, so a faceless character reports 20 and sails through both guards
while its visual age stays 0. The campaign age and the visual age are different numbers, and only the
visual one is wrong.

Ten cultures shipped this way: dale, dunland, gondor, harad, isengard, khand, lothlorien, mordor,
rhun, rohan, 46 characters in total. Only four of those ten can actually be reached in game
(gondor, isengard, mordor, lothlorien); the other six are vanilla-id reskins whose practice entries
never resolve at all, for the separate reason in
[tournament-armor-assignment.md](tournament-armor-assignment.md) "Six of those entries never
resolve". The player report came from a Gondor arena, which is one of the four. The nine cultures authored later already carried
`<face><face_key_template value="BodyProperty.fighter_<culture>" /></face>`, which is also what
vanilla's own `gear_practice_dummy_empire` does (`BodyProperty.guard`).
[CharacterFaceCoverageTests](../../TAOM.Tests/Core/CharacterFaceCoverageTests.cs) is the gate: every
`NPCCharacter` under `Main/_Module/ModuleData` must declare a `<face>`.

One thing this did **not** explain: how a practice character reaches the arena roster in the first
place. In stock 1.4.8 these entries are equipment donors only. `weapon_practice_stage_N_<culture>` is
read for `.BattleEquipments` (`ArenaPracticeFightMissionController.AddRandomWeapons`),
`gear_practice_dummy_<culture>` for `.RandomBattleEquipment` (`DefaultTournamentModel`, and TAOM's
override), and `CultureObject.GearDummy` is parsed from XML and then used by no shipped code at all.
Both roster builders draw from the town garrison and the culture's basic-troop upgrade tree, and a
sweep of the whole install found no upgrade edge, party-template stack or recruitment pool that
reaches one. The screenshot proves they do spawn; the path is still open. Fixing the face makes them
render correctly wherever that path is.

## How to Add a Tournament Armor Set for a New Culture

1. Open `Main/_Module/ModuleData/characters/npcs_<culture>.xml`.
2. Add/edit a `<NPCCharacter id="gear_practice_dummy_<culture>" …>` with an equipment block using skeleton-appropriate items.
3. Give it a `<face>` block, normally `<face><face_key_template value="BodyProperty.fighter_<culture>" /></face>` to match its siblings. Skipping it is the toddler bug above.
4. Verify item IDs exist in `LOTRLOME_Armory` (missing items → underwear). Run `python tools/validate_moduledata.py`.
5. No code changes: the model resolves the new dummy via `ResolveDummyId` on the next tournament.

## How to Add a New Race to the Dwarf-Dismount Set

If another custom-skeleton race is ever a tournament participant and clips inside mounts, extend the check in [TournamentService.ShouldDismountInTournament](../../Main/Features/Arena/TournamentService.cs) (e.g. compare against a small set of race names instead of the single `DwarfRaceName` constant). Add a unit test mirroring `ShouldDismountInTournament_DwarfRace_ReturnsTrue`. Non-humanoid creatures (spider, etc.) are **troops**, not tournament heroes, so they never hit this path — see memory `nonhumanoid-creature-troop-not-mount`.

## Changelog

- 2026-10-02: Prize pools capped at heavy. The bands are armour classes now (regular light and medium, elite heavy), weapons judged by engine tier, so a big tournament no longer awards Tier5 or Tier6 weapons and does award heavy armour (the armour gate's `NotMerchandise` flip had kept heavy out of every pool). Troll gear and elite, lord and named kit are never prizes, and an empty culture pool falls back to every culture's instead of to vanilla's fixed Calradian list.
- 2026-09-06: Arena practice characters rendered as toddlers. 46 `NPCCharacter` entries across ten cultures had no `<face>`, so the engine gave them body properties with age 0 and picked the toddler skin. Added the missing `face_key_template` to each and `CharacterFaceCoverageTests` as the gate. Data only, no code change.
- 2026-06-09 — Patch46 dwarf dismount added (`fix(arena)`, #277): postfix on `PrepareForMatch` clears Horse/HorseHarness for dwarf participants so they never spawn inside the mount; same-day hotfix corrected the injected `_match` field from three underscores to four (`____match`) after it crashed every campaign load.
- 2026-05-14 — Phase 9b: decision logic extracted from `TaomTournamentModel` into `ITournamentService` (`CalculateStartChance`/`CalculateEndChance`/`BuildPrizePool`/`ResolveDummyId`), registered via new `ArenaIoC`; model is now a thin boundary (#137).
- 2026-03-31 — Tournament model overhaul (#52): increased tournament frequency (lord-count step curve, 20-day end grace), culture-specific prize pools scanned from `Items.All` by culture + Tierf, and per-participant culture armor via `GetParticipantArmor`; the `gear_practice_dummy_<culture>` rosters that feed it had `civilian="true"` removed and a missing Lothlórien entry added (#51).

## GitHub Issue

- **Patch46 dwarf dismount:** [#277 — fix(arena): dwarves spawn inside the horse as tournament cavalry](https://github.com/haterade22/TAOM/issues/277) (2026-06-09). RCA: [docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md](../reviews/rca-tournament-dwarf-dismount-2026-06-09.md).
- **Original culture-armor model:** predates the mandatory issue-per-feature policy.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/tournament-armor-assignment.md](./tournament-armor-assignment.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/npcs-notables-and-townsfolk.md](../modding/npcs-notables-and-townsfolk.md)
- [docs/modding/troubleshooting.md](../modding/troubleshooting.md)

<!-- backlinks-end -->
