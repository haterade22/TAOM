# Player Switcher

Issue [#514](https://github.com/haterade22/TAOM/issues/514). Branch `feat/player-switcher`.

## Overview

At the character creation face generator, a panel lists the existing lords of the culture you
picked, in three groups: the ruling house, the clan leaders, and the wanderers. Choose one and you
play the campaign as that lord, with their face, gear, skills, clan, fiefs and kingdom. The
character you built is set aside. The backstory questions are skipped for you, straight to the
career choice, because they only ever applied to the character being set aside.

This is a reimplementation of a feature LOTRAOM shipped on Bannerlord 1.2.12. Two of its assets
crossed into this repo years ago and sat unused: the picker prefab
(`Main/_Module/GUI/Prefabs/FacGen/PreBuildCharacterSelection.xml`) and the race-aware face
generator transpiler (`Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs`,
which is what makes a non-human preview render animated rather than in a bind pose).

## Why this is a reimplementation and not a port

The old 15-file feature does not build on 1.4.8. Three of its seams no longer bind:

- `BodyGeneratorView`'s constructor gained a 13th parameter (`FaceGenHistory`). Harmony matches
  constructor overloads by exact type array, so the old 12-type attribute matches nothing and
  throws at `PatchCategory` time, which bricks startup.
- `CharacterCreationContentBase`, `SandboxCharacterCreationContent` and
  `StoryModeCharacterCreationContent` are gone. Stage flow moved to `CharacterCreationManager`,
  and per-mode content subclassing became a priority-ordered `ICharacterCreationContentHandler`
  registry.
- `ChangeKingdomAction.ApplyByJoinToKingdom`'s third positional slot is now a `CampaignTime`, so
  the old `ApplyByJoinToKingdom(clan, kingdom, true)` does not compile.

Roughly half the rest duplicates capability TAOM already ships better, and three pieces were
deliberately dropped. See "What was dropped" below.

## The design in one number: handler priority 1100

`PlayerSwitchContentHandler` registers at 1100. Vanilla's core handler is 800, StoryMode 900,
NavalDLC 1000, and TAOM's own `CharacterCreationRegistrationBehavior` is 1050.

`CharacterCreationManager.ApplyFinalEffects` runs, in order: `Clan.PlayerClan.Renown = 0f`, then
`CharacterCreationContent.ApplyCulture` (which rewrites `Hero.MainHero.Culture`,
`Clan.PlayerClan.Culture`, calls `ResetPlayerHomeAndFactionMidSettlement()` and rewrites
`BornSettlement`), then the selected narrative options, then the trait XP update, then the culture
start-point teleport, then the handler loop in priority order.

At 1100, every one of those effects and every TAOM grant (`SetPlayerRace`, `AssignCareer`, startup
gold, starting equipment) has already landed on the **throwaway** character-creation hero and the
**throwaway** `player_faction` clan, both of which the handover deletes moments later. The lord
being taken over is never touched by any of it.

That is why this feature needs no `BornSettlement` repair, no party reposition, and **zero edits
inside the 41-file `Main/Features/CharacterCreation/` module**.

**One caveat, found by the Codex review and worth knowing.** The isolation is not total. Vanilla
SandBox ships `HeirSelectionCampaignBehavior`, which listens to the same player-character-changed
events `ChangePlayerCharacterAction` fires. It snapshots the old main party's `ItemRoster` and the
old player's battle and civilian equipment, then adds both to the new main party. So TAOM's startup
equipment does not die with the throwaway hero: it arrives in the lord's party as loose inventory.
That is a cosmetic-ish gain rather than a correctness problem, and it is left alone. What it DID
cause was real: on the adoption path vanilla had already moved the items, so `AbsorbOriginalParty`
adding them again doubled every stack. That method now transfers members and prisoners only.

**Registering any lower would apply all of it to a real lore clan.** Below 1050, Erebor would start
at renown zero with a relocated home settlement, and `SetPlayerRace` would overwrite Sauron's
`race="sauron"` with a culture default, which `RacePersistenceService` would then faithfully
persist forever.

`PlayerSwitchRegistrationBehavior` wraps its registration in try/catch. `_handlers` is a
`SortedList<int, ICharacterCreationContentHandler>` and registration is a plain `Add`, so a
duplicate priority throws `ArgumentException` from inside `OnCharacterCreationInitializedEvent`
dispatch, which would take character creation down entirely. Degrading to "the switcher is
unavailable" is always the better trade.

## The handover, and why its order is load-bearing

`HeroSwitchService.Execute` runs the sequence below. `HeroSwitchServiceTests` pins it with
`Received.InOrder`, because two of the orderings are invisible at the call site and silently
corrupt a save if reversed.

**Takeover (`AssumeIdentity`, the target already has a clan):**

1. `Capture` a `SwitchTicket`: original hero id, original clan id, original party id, target clan
   id, career id. Must be first; after the swap, `Hero.MainHero` and `Clan.PlayerClan` no longer
   describe the character the player built.
2. `ApplyPlayerCharacter` calls `ChangePlayerCharacterAction.Apply`.
3. `ReassignPlayerClan` writes `Campaign.PlayerDefaultFaction`. **This must precede step 6.**
4. Optional gold transfer.
5. Career re-key, then `MarkClanAndKingdomKnown`.
6. `RemoveOriginalHero`. **This must follow step 2.**
7. `ClearPendingNotifications`.

**Why 3 before 6.** `KillCharacterAction.ApplyInternal` line 133 guards clan destruction on
`victim.Clan != Clan.PlayerClan`. Once the player clan pointer has moved to the target clan, the
throwaway clan is no longer `Clan.PlayerClan`, the guard passes, and
`DestroyClanAction.ApplyByClanLeaderDeath` runs. Reverse the two and the campaign keeps an orphan
empty clan forever, which is save-visible.

**Why 6 after 2.** `KillCharacterAction` takes its `victim == Hero.MainHero` branches and would run
`MakeDead` against the live player character.

**The leftover party is swept for free, but only on this path.**
`ChangePlayerCharacterAction.Apply` hands the character-creation party to the new main hero via
`LordPartyComponent.ChangePartyOwner` when it still holds troops, and destroys it only when the
roster is empty. `ChangePartyOwner` is `internal` (a mod cannot call it) and it does **not** move
`MobileParty.ActualClan`. So the leftover party stays registered to the throwaway clan, and
`DestroyClanAction.ApplyInternal` destroys every war party of the clan it dissolves. Nothing extra
is needed.

**Adoption (`AdoptIntoPlayerClan`, a clanless hero or wanderer):**

`AdoptIntoPlayerClan` sets `Occupation.Lord` then `Clan.PlayerClan.SetLeader(hero)` (which assigns
`leader.Clan` itself). The player keeps the clan they named and the banner they designed, so there
is **no** `PlayerDefaultFaction` reflection on this path at all.

Because the throwaway clan IS the player's clan here, it is never destroyed, so the leftover party
is **not** swept for free. `AbsorbOriginalParty` transfers its rosters into the player's party and
destroys it, after the original hero has been removed from that roster, and always by the single
captured party id.

> The predecessor mod did this with a predicate over the clan's war parties whose operator
> precedence made its second clause match every OTHER lord's party in the clan. Applied to a royal
> clan that would have merged and deleted all of them. Never sweep by predicate here.

**One asymmetry worth knowing.** On the adoption path the created character's gold reaches the
player anyway, regardless of the MCM knob, because `KillCharacterAction` line 98 gives a non-leader
clan member's gold to their clan leader on removal, and after adoption the player IS that leader.
This is left as it is: it is your own clan's money, not a windfall on top of a stranger's treasury.

## Skipping the backstory questions

Picking a lord used to leave the player answering all six backstory menus (parent, childhood,
education, youth, adulthood, age selection) before reaching the career choice. Every one of those
answers grants skills, attributes and traits to the character-creation hero, and the handover above
deletes that hero at finalize, carrying across only the career. So the player was filling in a
questionnaire for a character who was about to be thrown away.

`Patch78_PlayerSwitcher_CareerFastPath` postfixes `CharacterCreationManager.StartNarrativeStage()`
and, when `IPlayerSwitchSession.HasSelection` is true, walks the menu chain forward to the career
menu. The career menu is where the walk stops because it is the one choice in the stage that
survives: `SwitchPlanner` reads `ICareerMenuService.SelectedCareerStringId` and `HeroSwitchService`
re-keys it onto the lord.

Three things make this safe rather than clever:

- **There is exactly one caller.** `StartNarrativeStage` is called only from
  `SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView`'s constructor
  (v1.4.8), before it builds the stage ViewModel and before `LoadMovie`. The walk finishes before
  any UI exists to render the menus it passes, so nothing flickers.
- **The walk drives the real transition.** `GetSuitableNarrativeMenuOptions` →
  `OnNarrativeMenuOptionSelected` → `TrySwitchToNextMenu`, all public, no reflection. This is not a
  stylistic choice: `TrySwitchToNextMenu` opens with `SelectedOptions[CurrentMenu].OnConsequence(this)`,
  an indexer that throws `KeyNotFoundException` on a menu nothing was selected for. Selecting each
  hop also leaves `SelectedOptions` populated for the review stage and the trait XP pass.
- **Every failure is soft.** A menu offering no options, a refused advance, or a chain that never
  reaches career all abort the walk with a log line and leave the player in the ordinary backstory
  flow. That is the pre-fast-path experience, never a broken one.

The gate is re-evaluated on every entry to the stage, because the view is rebuilt each time.
Deselecting the lord restores the full questionnaire; re-picking restores the fast path.

**Two consequences worth knowing.** The six auto-picked answers still appear as rows on the review
screen, which is cosmetic (their effects landed on the discarded hero). And pressing Back from the
career menu walks back into those auto-answered menus one at a time rather than returning to the
picker, because `TrySwitchToPreviousMenu` is vanilla's own one-hop reverse. Neither is a fault; both
are the honest shape of driving vanilla's chain instead of jumping over it.

## Eligibility

`HeroPickerService` owns every rule and is engine-free, so all of it is unit tested.

| Rule | Reason |
|---|---|
| Culture must match | The adapter may over-return; the service filters |
| Never the current player, a child, or a notable | Notables anchor settlement issues; vanilla asserts when one is removed holding an issue quest |
| Placeholder names filtered | TAOM ships at least four heroes whose names contain "place holder" |
| One hero appears once | The ruling house wins over the clan-leader list, so a ruler who also leads their clan is not listed twice |
| Lore-locked heroes hidden by default | See below |
| Wanderers only when the MCM knob allows | Only 20 of 39 cultures have any |

**Lore-locked heroes.** Sauron and the Nazgul are opt-in and default off. Both `Patch76` hooks
return early for `Hero.MainHero` and defer to vanilla, so a player-controlled dark lord CAN be
captured and ransomed, which silently contradicts [uncapturable-heroes.md](uncapturable-heroes.md).
The MCM hint text states that consequence. **Do not "fix" this by adding a `Hero.MainHero` special
case to `Patch76`**: that would change vanilla player-captivity behaviour for everyone.

`HeroPickerAdapter` makes a single `Hero.AllAliveHeroes` pass with no `DeadOrDisabledHeroes` union.
The old mod unioned the two sets to catch not-yet-spawned wanderers, but
`CampaignObjectManager.OnHeroAdded` buckets into `DeadOrDisabledHeroes` only for `Dead` or
`Disabled`, so a `NotSpawned` wanderer is already in `AllAliveHeroes` and the union only risked
offering genuinely dead heroes.

## The UI

`Patch77_PlayerSwitcher`, two postfixes on `BodyGeneratorView`.

**Bind by arity, never by a type array.** The 1.4.8 constructor takes 13 parameters and is the only
one declared. `TargetMethods()` yields it only when `GetConstructors().Length == 1`, and
`Prepare()` refuses to bind otherwise. `AccessTools.Constructor(typeof(BodyGeneratorView))` with no
type array is **not** a substitute: Harmony normalises a null parameter array to `Type.EmptyTypes`
and looks for a parameterless constructor, which does not exist.

**The state guard is what keeps the panel off the barber screen** and the multiplayer face
generator, both of which construct the same view.

**Clearing on construction is the entire selection lifecycle.** The view is rebuilt every time the
player enters the face generator stage, so leaving to the culture stage and returning resets the
selection by itself. No patch on `ExecuteDone`, `ExecuteCancel` or `ResetFaceToDefault` is needed.
`OnFinalize` deliberately does **not** clear, because it fires when leaving the stage in either
direction and a selection must survive advancing forward.

**The prefab is used unchanged**, so the ViewModels were written to fit the file rather than the
other way round. `PlayerSwitcherPrefabContractTests` parses the shipped XML and asserts every
`DataSource`, `Text`, `IsSelected` and `Command.Click` name resolves to a public member, because
Gauntlet renders nothing for a missing binding and logs nothing about it.

`HeroPickItemVM` deliberately does **not** derive from vanilla `ClanPartyMemberItemVM`, though the
`ClanLordTuple` prefab was written for it and an earlier revision did.

That base takes `(Hero, MobileParty)` and its constructor opens with
`IsLeader = hero == party.LeaderHero;` with no null guard. A wanderer in a tavern has no
`PartyBelongedTo`, wanderers are offered by default, and one such row threw inside the panel build,
was swallowed by the attach patch, and made the entire picker silently fail to appear. Inheritance
bought a compile-time break on an engine change and cost the feature working at all for most
cultures. `HeroPickItemVM` therefore supplies the tuple's whole contract itself, and
`PlayerSwitcherBindingTests` pins the vanilla constructor's unguarded dereference so the reason
stays recorded. Both `OnCharacterSelect` and
`OnPreBuildCharacterSelected` route to the same handler, so the unresolved question of whether the
outer or inner click fires stops mattering.

**The preview** copies the lord's `BattleEquipment` into `BodyGeneratorView._dressedEquipment` slot
by slot. That field is `private readonly`, so it can never be replaced, only mutated, and a banner
in the extra weapon slot is cleared exactly as the view's own constructor does. `CanChangeRace` and
`CanChangeGender` are set to false **after** `SetBodyProperties`, never before.

**Restoring a preview also writes the body back.** The preview mutates the live `BodyGenerator`, and
vanilla calls `BodyGenerator.SaveCurrentCharacter()` from both `IFaceGeneratorHandler.Done()` and
`GoToIndex()`, which persists whatever is currently previewed into `CharacterObject.PlayerCharacter`.
Without an explicit save on restore, previewing a lord and then abandoning the selection left the
player wearing that lord's face on the character they had built. `RestoreDefault` now clears the
suppression flag FIRST (so the restoring refresh actually rebuilds the culture-filtered race
selector), restores the snapshot, and saves.

**The race repair, and why the preview needs one.** `FaceGenVM.SetBodyProperties` decodes the lord's
body-properties key and switches race, but it never applies the post-decode clamp that the engine's
own `BodyGenerator.InitBodyGenerator` applies: `FaceGenerationParams.SetRaceGenderAndAdjustParams`,
which bounds `CurrentVoice` (and hair, beard, textures, tattoo, eyebrow) to the target race's limits.
A lord whose key encodes a voice index his new race does not define therefore makes
`Refresh` throw inside `GetVoiceUIIndex`, part way through, before `UpdateFace` runs. `UpdateFace`
calls `BodyGenerator.RefreshFace`, the only assignment of `BodyGenerator.Race` outside the
constructor, so the face changes and the race silently does not.

The preview catches that, applies the engine's own clamp to the live params, and drives `Refresh`
directly. Directly, not through `SetBodyProperties` again, because a second call re-decodes the key
and undoes the clamp. This was found in game, on Isengard, where every `uruk_hai` lord failed and the
single `uruk` lord worked.

**Success is asked, not assumed.** The preview verifies `BodyGen.Race == row.Race` rather than
treating "no exception escaped" as success, and a preview that did not commit is rolled back through
`RestoreDefault`. That matters because `SetBodyProperties` assigns `CurrentBodyProperties` near its
top while race and gender are written later: a half-applied preview leaves the lord's body paired
with the player's race, and vanilla persists exactly that trio the next time `Done()` or
`GoToIndex()` calls `SaveCurrentCharacter`.

**The snapshot re-arms after every restore.** Otherwise previewing a lord, deselecting, editing your
own face, then previewing another and deselecting would restore the ORIGINAL snapshot and silently
discard the edits made in between.

`Patch9_RaceFilter` gains one early return keyed on `IPlayerSwitchSession.IsPreviewActive`.
`SetBodyProperties` triggers `Refresh(clearProperties: true)` on every race change, so without it
the culture race rebuild would snap a dwarf or Sauron preview straight back to the culture default.

**Sprites.** `SpriteCategory` carries an `IsLoaded` bool and **no reference count**, so `ui_clan` is
loaded when absent and then **never unloaded**. "I loaded it" is not ownership: if any other screen
starts using the category while the picker is open, its own `Load()` is a no-op, and unloading on
teardown would release the textures out from under it with no error and no log line. A resident
vanilla sprite sheet is the cheaper mistake.

## Two refusals worth knowing

**The startup clan must be disposable.** The takeover leaves the character-creation clan behind and
relies on vanilla destroying it when its leader is removed. That only happens when the created hero
is the last lord in it. StoryMode seeds the same clan with an adult elder brother, so
`KillCharacterAction` promotes him via `ChangeClanLeaderAction` instead, the clan survives, and the
leftover character-creation party survives with it. `IPlayerIdentityAdapter.StartupClanIsDisposable`
is checked before any mutation and the takeover is refused when it is false. This gates StoryMode
out without naming StoryMode, which is the more durable form of the rule.

**A failure after the swap is never reported as a failure.** The engine offers no transaction. Once
`ChangePlayerCharacterAction.Apply` has run, `Game.Current.PlayerTroop` has changed and the
player-character-changed events have been dispatched to every listener; there is no rollback. The
handover therefore tracks whether it crossed that point, and a later exception yields
`SwitchOutcome.SwitchedWithErrors` rather than `Failed`. The distinction is not cosmetic: the
`Failed` message tells the player they are continuing as their own character, which after the swap
would be a lie.

## Reflection sites

Two, both catalogued in `docs/reference/taleworlds-api-snapshot/reflection-sites.md`.

| Site | Why | Failure behaviour |
|---|---|---|
| `Campaign.PlayerDefaultFaction` (`PlayerIdentityAdapter`) | `internal { get; set; }`; `Clan.PlayerClan` is a computed getter over it and `ChangePlayerCharacterAction` never updates it | Probed once at construction, before any UI exists. A failed probe disables the feature for the session rather than leaving a campaign half-swapped |
| `BodyGeneratorView._dressedEquipment` (`BodyGeneratorPreviewSink`) | `private readonly Equipment` | Soft. The preview renders undressed and character creation continues |

## Save compatibility

**None needed.** No `SaveableTypeDefiner`, no new save keys, no base id consumed off TAOM's `+100`
ladder. Everything durable rides existing systems: identity on `Game.PlayerTroop` and
`Campaign.PlayerDefaultFaction` (both already engine-serialised), the lord's race on both their own
template `race=` attribute and `RacePersistenceService`, the career on `ICareerDataService` keyed by
`StringId`. Every code path runs only during the character creation of a new campaign, so existing
saves are untouched and the resulting save is structurally the same shape as one made after vanilla
heir succession.

One accepted cost: the throwaway hero's career row is left in the career store rather than deleted,
because `ICareerDataService` has no `ClearCareer` and adding one would reach into another feature
for one orphan dictionary entry.

**A consequence worth knowing: nothing persists the fact that a takeover happened.** The reassigned
`Campaign.PlayerDefaultFaction` survives, but the knowledge that TAOM moved it does not, so any rule
that needs to distinguish "playing an existing lord" from "vanilla start" cannot key on this feature's
own state. The durable proxy is the clan id itself: vanilla creates exactly one clan for the player and
names it `player_faction`, so `Clan.PlayerClan.StringId != "player_faction"` means a takeover. AI party
size scaling uses precisely that, see below.

## Interaction: AI party size

A taken-over clan is handed rosters that vanilla's new-game top-up filled at world generation, against
the AI-scaled party size limit, while the clan was still AI. The handover then moves `Clan.PlayerClan`
onto it and `AiPartySizeService` stops scaling it, so the cap collapses and thousands of men shed over
the following days. Observed on a Gondor takeover: 11,400 men against real limits totalling ~1,100
([#530](https://github.com/haterade22/TAOM/issues/530)).

Since 2026-09-01 the `Apply Party Size To Player Clan` MCM setting defaults to `Taken-over lords only`,
which makes a taken-over clan **eligible** for the scaling and leaves an ordinary start untouched.
Eligibility is not an effect: the numeric knobs also ship neutral (multiplier 1.0, flat bonus 0), so
at stock settings a taken-over clan's inherited rosters still collapse to the vanilla cap exactly as
#530 reported. Both sliders have to be raised for the fix to do anything. Food and wage
relief are never granted to a player clan at any setting. Details and the engine evidence:
[ai-party-size.md](ai-party-size.md) "Player clans". #530 stays open for the `Never` case, where the
reduction is still silent and cache-timed rather than visible at the handover.

## Interaction: kingdom votes, when the player is not their clan's leader

The picker offers a king's spouse or child as its `RulingHouse` group, checked BEFORE `IsClanLeader`
([HeroPickerService.cs:59](../../Main/Features/PlayerSwitcher/HeroPickerService.cs#L59)). Those heroes
have a clan, so [SwitchPlanner.cs:17](../../Main/Features/PlayerSwitcher/SwitchPlanner.cs#L17) routes
them down `AssumeIdentity`, which sets `Hero.MainHero` and reassigns `Clan.PlayerClan` but never
reassigns clan leadership. `Clan.SetLeader` is called in exactly one place,
`PlayerIdentityAdapter.AdoptIntoPlayerClan` (:105), and that is the clanless path.

So after taking over a queen or a non-heir prince, **`Clan.PlayerClan.Leader` is still the AI king**,
a different `Hero` from `Hero.MainHero`. That is not cosmetic. Vanilla keys player identity in a
kingdom election off the CLAN LEADER, not off the player's clan:
`Supporter.IsPlayer => Clan.Leader.IsHumanPlayerCharacter`. For such a player `IsPlayerSupporter` is
false for every decision, permanently, so `KingdomElection.StartElection()` takes the
`ReadyToAiChoose()` branch and resolves each decision synchronously inside
`DecisionItemBaseVM.InitValues()`, before the view model has ever been bound to a widget. The window
then renders for a vote that is already over, and the popup's auto-close edge can be missed, leaving
it unclosable with map navigation locked.

Tracked as [#550](https://github.com/haterade22/TAOM/issues/550). **`Patch80_KingdomVoteDeadlock`
(#547) does NOT cover this**: all three of its seams gate on `ShouldBeCancelled()` / `IsCancelled`, and
`ReadyToAiChoose()` never sets `IsCancelled`. Two candidate fixes are laid out in the issue; the
root fix (make the player their own clan's leader on takeover) would also correct every other vanilla
path keyed on `Clan.Leader.IsHumanPlayerCharacter`, of which there are many.

Anything else that reads `Clan.Leader.IsHumanPlayerCharacter` rather than `Clan == Clan.PlayerClan` is
suspect for these players and has not been swept.

## What was dropped, and why it must stay dropped

| Dropped | Reason |
|---|---|
| `KeepHeroRaceCampaignBehavior` + its `SaveableTypeDefiner` | Would race the shipped `RacePersistenceService` over `OnBeforeSave` and `OnSessionLaunched`, and burn a base id. The shipped service already covers the case and carries three post-ship bug fixes the old one does not |
| `NazgulEditDisablePatch` | Keys on FaceGen race `"nazghul"`. TAOM has no hero at that race (six of the Nine carry no race attribute, three carry `uruk`), so it would compile, run, and match nothing. Any equivalent lock keys off `IUncapturableRegistry` |
| The `WarPartyComponent` sweep | Live operator-precedence bug, described above |
| The five `FaceGeneratorVMPatch` patches | Replaced by one early return in `Patch9_RaceFilter` |
| The heirless-leader eligibility rule | Unreachable. Adoption only ever targets clanless heroes, and a clanless hero leads no clan, so no clan is ever left with a dangling `_leader`. Do not restore it without a case that can actually reach it |

## Configuration

`[SettingPropertyGroup("Player Switcher")]` in `Main/Features/TaomSettings.cs`, read only through
`PlayerSwitchPolicyProvider`.

| Setting | Default | Note |
|---|---|---|
| `EnablePlayerSwitcher` | `true` | Off means the movie never loads and the handler no-ops |
| `PlayerSwitcherIncludeWanderers` | `true` | Only 20 of 39 cultures have any |
| `PlayerSwitcherAllowLoreLockedHeroes` | `false` | Hint text states the capture caveat |
| `PlayerSwitcherTransferStartingGold` | `false` | An established lord is already funded |

All four are simulation-relevant for co-op under the include-by-default rule, and are counted in
`SettingsFingerprintTests`.

## Interactions checked

- **Co-op possession.** Closed by ordering rather than a flag. `PlayerPossessionBehavior` captures
  its choices on `OnCharacterCreationIsOverEvent`, which fires **after** the 1100 handler, so it
  records the lord and its own `currentHeroId == _choices.HeroId` guard suppresses the re-grant.
  Do not "fix" this by adding a switcher flag to `PlayerPossessionService`.
- **Landless cultures.** `Patch65` guards the AI daily clan tick, which never runs for
  `Hero.MainHero`, so no new crash surface. Any fallback chain must still tolerate a null clan home
  settlement.
- **Enlistment.** Cannot collide: no enlistment record can exist during character creation.

## Verification

```
dotnet test TAOM.Tests --filter FullyQualifiedName~PlayerSwitcher
dotnet test TAOM.Tests
./build.ps1 -RunTests                                                  # game closed
dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=   # game running
```

### In-game smoke

Each step on a fresh campaign unless stated.

1. **Feature off.** Master toggle off, run character creation through to the map. Correct: no
   panel, no behavioural change, no log noise. Proves both patches are inert.
2. **Panel and preview.** Toggle on, new campaign, pick Erebor. The panel lists three groups. Click
   Dain. Correct: the model becomes a dwarf, animated rather than in a bind pose (this exercises
   `RefreshCharacterEntityAuxPatch`), wearing his own gear; race and gender controls grey out.
   Click him again: the preview reverts and the controls re-enable.
3. **Back and forward.** Go back to the culture stage and return. Correct: the panel rebuilds, the
   selection is cleared, the created character is intact.
4. **The takeover.** Select Dain and finish creation. Correct on the map: you are Dain; the
   character screen opens without a crash (this is the exact `CharacterDeveloperVM` failure the old
   mod documented); the clan screen shows his family, not "unknown"; the kingdom screen shows
   Erebor with you as ruler; his fiefs are yours; **his clan's renown and tier are non-zero**; your
   party sits where his was, with his troops; your career is the one you picked; the old
   `player_faction` clan is gone from the clan list.
5. **A non-ruling clan leader.** Same flow. Correct: you lead that clan as a vassal of Erebor, and
   both screens are coherent.
6. **Race survives a save cycle.** Save, quit to the main menu, reload. Correct: still Dain, still
   a dwarf, career intact.
7. **Lore-locked gate.** With the toggle off, Sauron is absent from Mordor's list and Khamul from
   Dol Guldur's. Turn it on and they appear. Play as Sauron. Correct: the campaign boots and
   `rgl_log` shows the priority-1100 handler line with **no** `SetPlayerRace` line naming Sauron.
8. **Co-op non-regression.** With a co-op mod loaded, repeat step 4 and let several in-game hours
   pass. Correct: no `[Possession] Controlled hero changed` line and no re-grant.
9. **Wanderer adoption.** Pick a wanderer. Correct: your clan is the one you named, with your
   banner, you lead it, and your party holds the starting troops.
10. **Kingdom join.** Accept the prompt that follows step 9. Correct: your clan joins and the
    kingdom screen agrees. The prompt must NOT appear after step 4, nor after an ordinary character
    creation with no lord selected.
11. **Barber screen.** Load the step 4 save and visit a barber. Correct: no picker panel.
12. **Degrade path.** Force the reflection probe to fail. Correct: the panel never loads and
    character creation completes normally.
13. **The fast path.** CONFIRMED 2026-09-03. Pick a lord and click Next. Correct: the first screen after the face
    generator is the career menu, with no flicker of the six backstory menus. `rgl_log` shows
    `skipped 6 backstory menus`. Pick a career and finish: the lord arrives on the map with it.
14. **The fast path is opt-in by selection.** Repeat with NO lord picked. Correct: all six
    backstory menus appear exactly as before. This is the regression check that matters most.
15. **Back-navigation.** From the career menu press Back repeatedly: the auto-answered menus appear
    in reverse order (expected, see above). Return to the face generator, deselect, go forward:
    the full questionnaire is back. Re-pick and go forward: the fast path runs again.
16. **A low-population culture.** Any culture whose narrative menus are sparse. Correct: either the
    fast path runs, or it aborts with a logged warning and the player continues through the normal
    questions. A crash here would mean the zero-option abort is missing.

## In-game verification (2026-08-28)

First real runs. Evidence is from `taom_debug_2026-08-28_*.log`; anything not listed here is still
unexercised, and the distinction is deliberate.

**Confirmed by log, not by inference:**

| Claim | Evidence |
|---|---|
| The 1100 ordering does what it was designed for | At `11:06:17` the 1050 grants land on the throwaway hero (`Set player race to 'uruk_hai'`, `assigned career 'uruk_berserker' to hero 'main_hero'`, starting equipment), and only then `Player Switcher: player is now 'lord_I1_1' via AssumeIdentity` |
| The takeover completes and reaches the campaign map | Same session, no `[ERROR]` line anywhere in it |
| The career re-key works | `assigned career 'uruk_berserker' to hero 'main_hero'` followed by `assigned career 'uruk_berserker' to hero 'lord_I1_1'`, then `RefreshCareerState hero='lord_I1_1' HasCareer=True unspent=29` |
| Co-op possession is closed by ordering, as designed | `[Possession] Captured character-creation choices for 'lord_I1_1'` records the LORD, not the throwaway hero, because it fires on `OnCharacterCreationIsOverEvent` after the 1100 handler. This is smoke step 8, passed without needing a co-op session |
| A non-human lord renders correctly | `Patch2 race=5 monster='uruk_hai' 'as_uruk_hai_warrior'`, 122 skeleton meshes, `agentVisible=True` |
| The preview switches race, not just face | Confirmed by the user on the post-fix build, after the `SetRaceGenderAndAdjustParams` repair landed |
| Deselect restores the player's own character | Confirmed by the user on the same build |

**A caveat worth keeping:** the successful takeover above ran at 11:06 on a build that predated the
race repair. The repair changed the preview, not the handover, so the handover evidence stands, but
the takeover has not yet been repeated end to end on the current build.

## Owed

The three checks that still matter most, because they exercise the reflection write that the whole
takeover rests on:

1. **Open the character screen after a takeover.** This is the `CharacterDeveloperVM` crash the
   predecessor mod documented, caused by `Clan.PlayerClan` still pointing at the abandoned clan.
   Landing on the map does NOT exercise it.
2. **Open the clan screen.** It should show the lord's family rather than unknown entries, which is
   what `MarkClanAndKingdomKnown` exists for.
3. **Check renown and tier are non-zero and the old `player_faction` clan is gone** from the clan
   list. Both depend on the reassign-before-remove ordering.

Then the paths never run at all: **wanderer adoption**, the **kingdom-join offer** that follows it,
the **lore-locked gate** (Sauron and the Nazgul, MCM off then on), the **barber screen** guard, and
**race surviving a save cycle**.

- **Hero states are not filtered.** `Hero.AllAliveHeroes` excludes dead and disabled heroes but
  still includes prisoners, fugitives, released and `NotSpawned` heroes, and the picker does not
  inspect `HeroState`, `DeathMark`, or eliminated-clan status. Taking over a prisoner would begin
  the campaign in captivity, because `Campaign.OnPlayerCharacterChanged` recognises the captor.
  The Codex review raised this as SUSPECTED and could not establish reachability against shipped
  TAOM startup data; neither could I. **Probe it first in the smoke**: if a prisoner or a
  `NotSpawned` hero can appear in the list, add the state filter before shipping.
- The machine translation. The 15 keys are seeded with English in all twelve languages, so the game
  renders real text rather than a raw id, but no API key was available in the authoring session.
  The translator's own filter treats a row equal to English as untranslated, so a later
  `tools/translate_with_claude.py` run picks all 15 up.

## See also

[character-selection.md](character-selection.md) (the shipping race-aware transpiler that makes a
non-human preview render), [uncapturable-heroes.md](uncapturable-heroes.md),
[coop-interop.md](coop-interop.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/character-creation.md](./character-creation.md)
- [docs/features/character-selection.md](./character-selection.md)
- [docs/modding/wanderers-and-named-companions.md](../modding/wanderers-and-named-companions.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
