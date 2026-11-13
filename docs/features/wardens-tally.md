# Warden's Tally

## Overview

The town and castle dungeon menus say how likely a captive lord is to slip out. Under vanilla's own sentence ("There are N prisoners here.") the menu now adds how many prisoners the dungeon holds against its capacity and how many of them are lords, each lord's real daily escape chance with the terms behind it (your clan holding him halves it; the governor's perks change it), a line for every lord of a kingdom with a War Chronicle prisoner-escape boost, and a line for every lord the engine will not let escape. It is display only: no rule changes, nothing is saved.

## Why This Exists

- **Vanilla behavior:** the dungeon menu shows one sentence with the number of prisoner lords. The daily escape roll is private (`PrisonerReleaseCampaignBehavior.DailyHeroTick`, v1.5.6 `:201-250`) and nothing on screen says what it is.
- **TAOM requirement:** a player holding a Gondor lord in a Rohan dungeon wants to know whether to ransom, move or release him (#903). TAOM also adds an extra escape roll for boosted kingdoms (the War Chronicle, [war-chronicle.md](war-chronicle.md) "Consumer 2: prisoner escape"), which vanilla's numbers would not show.
- **Without this feature:** the player guesses, and a display built from vanilla's constants alone would ignore the veto and the boost.

## What the player sees

Opening the dungeon from a town's keep (`town_keep_dungeon`) or a castle (`castle_dungeon`), with the switch on and at least one prisoner lord held:

```
There are 3 prisoners here.
Held: 34 of 100, 3 of them lords.
Each lord's daily chance to escape: 1.5%
Base chance: 4.0%
Held by your clan: halved
Dungeon Architect: -25% of the base chance
Boromir: 4.5% a day with the war's escape boost.
Denethor cannot escape.
```

The figures above are a dungeon of your clan, a governor with Dungeon Architect, and one lord of a kingdom whose escape boost is at its largest, x3 (the multiplier is clamped to 1 to 3 in `WarEffectMath.cs`). His 4.5% is `1 - (1 - 0.015) x (1 - 0.03)`.

- **Held:** prisoners against the settlement party's prisoner capacity (`PartyBase.PrisonerSizeLimit`, 100 for a town or castle before any dungeon project), lords included, and how many are lords.
- **Each lord's daily chance:** `WarEscapeService.VanillaChance`, vanilla's own chance for a lord in this dungeon. It follows only when a lord can escape.
- **The terms:** shown when something besides the base applies. The base chance, "Held by your clan: halved" when the settlement is the player clan's, and one line per perk term, each by the engine's own description text (the perk name) and its signed change as a percent of the base chance ("-25%", "+10%", "-12.5%"). The engine adds the terms before it multiplies the base, so the changes add up: with "Held by your clan: halved" the chance is the base times 0.5 times (1 plus the sum of the changes). The terms are the governor's Sweet Talker, Dungeon Architect and Mounted Patrols for a town or castle captor.
- **One chance for all:** every lord of a dungeon shares the headline chance and its terms. A garrison's captures go to its home settlement's party, not to the garrison party (`MapEventParty.RosterToReceiveLootPrisoners`, v1.5.6 `MapEventParty.cs:95-98`), so the settlement is the captor of every lord in the dungeon and the settlement terms apply to all.
- **The boost:** one line per lord whose kingdom has a `PrisonerEscape` multiplier above 1 and who is eligible for the extra roll, with his combined daily chance `1 - (1 - p) x (1 - extra)`, where `p` is vanilla's chance and `extra` is `p x (multiplier - 1)`.
- **The veto:** one line per lord for whom `CanHeroBeReleased` is false.

Percents carry one decimal and a point, whatever the language, and the "%" sits in the text row so a translator can place it. Nothing new appears with no lords, with the MCM switch off, or in any menu but the two dungeon menus. With no lord vanilla's sentence says "There are no prisoners here." (it counts prisoner heroes only), so the menu is left as vanilla has it rather than put a count of regulars under that sentence.

## The rule: one source for the odds

The chance is not recomputed here. The binding decision on #903: show TAOM's real odds from the existing adapter and service, never a second copy of vanilla's constants.

- `WarEscapeService.VanillaChance(snapshot)` holds the one copy of the constants: vanilla's gate (a living prisoner, not the main hero, not vetoed), the base 0.04, the mobile-captor term, the player-held halving and the perk factor, and 0 when the factor is not positive. `ExtraChance` is `VanillaChance x (multiplier - 1)` for an eligible lord, so the War Chronicle pass is unchanged (its tests did not change). `BaseChanceOf` gives the base before the halving and the factor.
- `PrisonerEscapeAdapter` builds the perk terms in one place, `EscapeTerms(hero, captor, includeDescriptions)`. `EscapeFactor` is that `ExplainedNumber`'s result; the menu's lines are `TermLines` of the same number built with descriptions, so the factor and the lines cannot differ.
- `IPrisonerEscapeAdapter.ReadDungeon(settlementId)` returns a `DungeonReading`: the regulars and heroes held, the capacity, and for each prisoner hero his name, `PrisonerEscapeSnapshot` and term lines. It reads the same parties as vanilla's count (`SettlementComponent.GetPrisonerHeroes`, v1.5.6 `:158-185`): the settlement's own party and each garrison party in it.

## The boost line follows the pass

`IWarEffectService.GetMultiplier(kingdomId, WarEffectKind.PrisonerEscape)` is the multiplier, with eligibility from `WarEscapeService.IsEligible`, as `WarEscapeDailyPass` does. What gates the pass, read in the code:

- `WarChronicleBehavior.OnDailyTick` runs `WarChronicleTickService.RunDaily` (and with it `WarEscapeDailyPass.Run`) only when `ICoopSessionProvider.IsAuthority` is true and `CoopSessionPolicy.MayWriteSaveBackedState(IsCoopClient)` is true. There is no MCM switch on the escape pass itself: `War Effect Strength` scales the multiplier (0 bakes it to 1), and the rally switch only removes the rally's own effects.
- So `WardensTallyMenuService` reads those two terms live on each call and passes a multiplier of 1 where the pass does not run. **A co-op client's dungeon menu shows no boost line**: its War Chronicle effect registry is the one it got from the join save and never updates ([war-chronicle.md](war-chronicle.md), "Co-op: host-only"). The host and a single-player game show it. Under BannerlordTogether `IsAuthority` fails open, so every peer runs the pass and every peer shows its line.
- **A co-op client shows no player-held halving either.** The server rolls the escape without it, so the menu clears `PlayerHeld` on a client's readings: a lord of your clan's dungeon shows the unhalved chance and no "Held by your clan: halved" line.

## Configuration

| Field | Value |
|-------|-------|
| Group | "World/Warden's Tally", `GroupOrder = 80` |
| Name | "Show Warden's Tally" (`WardensTallyEnabled`) |
| Default | on (`WardensTallySettingsProvider.DefaultEnabled`) |
| Restart | not required; read each time a dungeon menu opens |

The setting is new, so the default reaches every install. **Never flip the default** later: MCM's json2 keeps a stored value; rename the property instead. Under co-op the switch is classified as presentation (`CoopSettingsRelevance`), so it is not part of the settings fingerprint.

## Architecture

Entry point (thin) to service to adapter:

```
WardensTallyMenuBehavior   (CampaignEvents.GameMenuOpened, after the dungeon menus' init)
        |
WardensTallyMenuService   menu id, switch, session gate for the boost
        |                       |
IPrisonerEscapeAdapter     WardensTallyService  (pure: reading + multiplier lookup -> WardensTallyView)
 .ReadDungeon                   |
        |                  WarEscapeService (VanillaChance, BaseChanceOf, IsEligible, ExtraChance)
        |
WardensTallyTexts   (view -> TextObjects with {=taom_wt_*} keys; vanilla's sentence rebuilt under its own keys)
```

- **Mechanism.** `GameMenu.RunOnInit` (v1.5.6 `GameMenu.cs:315-326`) runs the menu's init delegate, copies the title, and then raises `CampaignEventDispatcher.OnGameMenuOpened` on the same `MenuCallbackArgs`. `WardensTallyMenuBehavior` listens to `CampaignEvents.GameMenuOpened` (a campaign behavior built per campaign, no Harmony patch). For `town_keep_dungeon` and `castle_dungeon` it runs only when `args.MenuTitle` is set, which `PlayerTownVisitCampaignBehavior.town_keep_dungeon_on_init` (v1.5.6 `:772-791`) does in the same branch that sets `PRISONER_INTRODUCTION`. It then sets that variable again, as a plain string: the engine re-renders a menu text that has attributes every frame (`GameMenuVM.IsMenuTextChanged`). A throw is logged once per behavior and leaves vanilla's text. `WardensTallyBindingTests` pins the init-before-event order.
- **Vanilla's sentence is rebuilt, not read.** `MBTextManager` has no getter for a text variable (its `TextProcessingContext` is private), so `WardensTallyTexts.VanillaIntro` rebuilds it under vanilla's own key `{=gAc8SWDt}`, with `PRISONER_COUNT` the number of prisoner heroes, so vanilla's translations apply. `WardensTallyBindingTests` pins the key and text against the installed IL.
- **The line break** is a local `NEWLINE` variable set on the intro text object, so it does not depend on a global.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/WardensTally/Hooks/WardensTallyMenuBehavior.cs` | The menu-opened listener |
| `Main/Features/WardensTally/WardensTallyMenuService.cs` | Menu ids, switch, the boost gate |
| `Main/Features/WardensTally/WardensTallyService.cs`, `WardensTallyView.cs` | The pure tally and its view model |
| `Main/Features/WardensTally/WardensTallyTexts.cs` | The words |
| `Main/Features/WardensTally/WardensTallySettingsProvider.cs`, `WardensTallyModule.cs` | The MCM switch, the feature module |
| `Main/Adapters/DungeonReading.cs` | The per-dungeon read model |
| `Main/Adapters/IPrisonerEscapeAdapter.cs`, `PrisonerEscapeAdapter.cs` | `ReadDungeon`, `EscapeTerms`, `TermLines` |
| `Main/Features/WarChronicle/Effects/WarEscapeService.cs` | `VanillaChance`, `BaseChanceOf` |
| `Main/Composition/FeatureModules.cs` | One line for the module |
| `Main/Features/TaomSettings.cs`, `CoopInterop/CoopSettingsRelevance.cs` | The setting and its co-op class |

## Dependencies

- `IPrisonerEscapeAdapter`, `WarEscapeService`, `IWarEffectService` (War Chronicle registrations, `WarChronicleIoC`): the odds, the veto and the boost. The module registers none of them.
- `ICoopSessionProvider` (CoopInterop): the live session read that gates the boost line.
- `TaomSettings` (MCM): the switch.

## Co-op

`WardensTallyMenuBehavior` runs on every peer and writes only a local text variable, so it is not gated. Only the boost line follows the pass (above). See the Warden's Tally paragraph in [coop-interop.md](coop-interop.md).

## Known limits

- The text is built when the menu opens. It is not rebuilt while the menu stays open.
- The reading includes any prisoners a garrison party holds, because vanilla's sentence counts them, but the capacity is the settlement party's alone. In v1.5.6 a garrison's captures go to the home settlement's party (`MapEventParty.cs:95-98`), so the garrison rosters are normally empty.
- On the last day of an effect the boost line shows a boost that will not roll: the daily pass runs after `Expire` (`WarChronicleTickService.RunDaily`), so at the next tick the effect is gone before the roll.
- A throw inside the menu's later text rendering is outside the listener's guard; the tally contains only numbers and the names the engine gives its heroes and perks.

## Tests

- `TAOM.Tests/Features/WardensTally/WardensTallyServiceTests.cs`: the pure tally (empty dungeon, no lords, the base only, the halving, each perk term and its sign, a vetoed lord, a boosted lord's combined chance, a boosted lord held in a siege or of the player's clan, NaN and negative multipliers failing closed, negative counts).
- `WardensTallyTextsTests.cs`: percent and signed-percent formatting, each line's English, the order, vanilla's rebuilt sentence and its key.
- `WardensTallyMenuServiceTests.cs`: the two menu ids, the switch, no lords, the co-op gate on the boost line and on the halving, a client with an unknown settlement.
- `WardensTallyMenuBehaviorTests.cs`: the listener's guard, the fault logged once, the variable read back (one lord and three lords, so the plural form is proven through the global variable), an empty `SyncData`.
- `WardensTallyBindingTests.cs`: the init's shape, both menus still use it, the title and variable order, vanilla's key and text, the parties behind the count, `RunOnInit` invoking the init before the event, and a garrison's captures going to its home settlement.
- `WardensTallyWiringTests.cs`: the module listed once, the single-owner files untouched, the behavior built per campaign over the container's menu service, the registrations resolving, no Harmony patch in the feature.
- `WardensTallySettingsProviderTests.cs`: the default, the group order, the restart flag, the hint, the co-op class.
- `TAOM.Tests/Features/WarChronicle/Effects/WarEscapeServiceTests.cs`: `VanillaChance`, `BaseChanceOf` and `ExtraChance` as `VanillaChance x (multiplier - 1)`; `TAOM.Tests/Adapters/PrisonerEscapeAdapterTests.cs`: `ReadDungeon` outside a campaign and the `ExplainedNumber` line shape.
- `SettingsFingerprintTests`, `ServerSettingsSchemaTests` and `CampaignHotPathSettingsProvidersTests`: the counts and the provider rows this feature moved.

## Translation

The 12 language files hold translated rows (2026-11-12, #763).

## How to verify in game

**Preconditions:** a save holding a lord prisoner in a town and in a castle; the MCM switch "Show Warden's Tally" on; for the boost line, the host or a single-player game.

1. Open a town's keep, then "Go to dungeon": under the engine's sentence the menu shows "Held: X of Y, N of them lords." and "Each lord's daily chance to escape".
2. Do the same in a castle.
3. In a settlement of your clan, the line "Held by your clan: halved" appears and the chance is half the base.
4. With a governor who has Sweet Talker, Dungeon Architect or Mounted Patrols and is present in the settlement (`PerkHelper.AddPerkBonusForTown` requires `governor.CurrentSettlement == town.Settlement`), the perk's name appears with its signed percent of the base chance.
5. Hold a lord of a kingdom with a boost (`taom.war_effect_add <kingdomId> PrisonerEscape 0.5 10` on the host): his combined-chance line appears; on a co-op client it does not.
6. Turn the switch off and reopen the menu: vanilla's sentence only.
7. On a co-op client, open the same dungeon: no boost line, and a lord of your clan's dungeon shows the unhalved chance.
8. Compare the stated chance with the lord's escapes over several days.

The veto line comes from vanilla's own listener, `ExecutionCampaignBehavior.CanHeroBeReleased` (v1.5.6 :480): a lord waiting for a settlement execution, or for an execution at the end of a battle, cannot escape. To see it, order a dungeon lord's execution and open the dungeon before it is carried out. TAOM adds no listener of its own.

An unreachable step is reported with the command tried and the unmet precondition.

## Changelog

- 2026-10-27: first version (#903).

## GitHub Issue

- **Issue:** #903
- **Status:** Open until the in-game check
