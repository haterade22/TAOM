# Bandit Voices

## Overview

Eight of TAOM's bandit clans speak in their own words. When a clan's warband robs the player, it threatens, haggles, takes the toll and starts the fight in lines written for that clan (two sets each). When the player attacks one of its parties on land and has not dealt with it before, there is a one in four chance that it opens with its own greeting instead of vanilla's, and the player's ultimatum is vanilla's reply to that kind of greeting. One MCM switch for the greetings, default on. Text only: nothing about the encounter, its odds or its rewards changes.

## Why This Exists

- **Vanilla behavior:** a bandit conversation is built from text vanilla keys by `MapFaction.StringId`, and it knows only its own five bandit factions (`forest_bandits`, `mountain_bandits`, `desert_bandits`, `steppe_bandits`, `sea_raiders`). TAOM replaced those cultures with Dunlending, Rhun, Haradrim, Gundabad and Umbar raiders plus the Gondor, Blacklock and Mirkwood offshoots (see [bandit-management.md](bandit-management.md)), so every TAOM bandit falls through to the generic lines: a Gundabad orc and a Gondor deserter threaten in the same words, in a male voice.
- **TAOM requirement:** a robbery by the Corsairs of Umbar should not read like one by hill-folk of Dunland.
- **Without this feature:** the clans are told apart by their banners and nothing else.

## What the player sees

- **Robbed.** When a bandit party catches the player (the player is the defender), the demand, the answer to an offer to bargain, the line after the toll is paid and the line when the fight starts come from the clan's own set. Vanilla picks from its six generic sets plus three copies of each clan set it finds, so with two sets the clan's own words are 6 of the 12 entries, half of the robberies; the generic sets stay in the mix.
- **Attacking.** On a first meeting with a party of one of the eight clans, on land (a party that robbed the player earlier, or was paid off, counts as met and never greets), a quarter of the time the party greets the player with its own line, either a claim to be harmless or a bluff about warriors hiding nearby. The player's ultimatum reply is vanilla's own for that kind of greeting. After the first conversation vanilla records the party, so it never speaks this way again.
- **Not touched.** Spiders of Mirkwood and wild trolls do not parley (Patch94). Looters stay vanilla, and so do the Feral Orcs, who have no voice rows ([feral-orcs.md](feral-orcs.md)). At sea, and on a repeat meeting, the greeting is vanilla's. The robbery pool does not check the meeting record, so a clan party robs in its own voice about half the time, on every meeting.

## Architecture

### Design Challenge

- Vanilla reads robbery text with no fallback: it asks for the threat under `<factionId>_<k>` for k from 1 to 6 and, when that exists, asks for the other three lines under the same variation (`BanditInteractionsCampaignBehavior.cs:534-542`). A variation missing one of its four rows would print `ERROR: Text with id ... doesn't exist` in the middle of a robbery (`GameTextManager.FindText`, `GameTextManager.cs:89-91`).
- The game-text loader splits an id on "." (`GameTextManager.cs:190`), so a variation id holds no dot.
- The first-meeting record is a private dictionary of party to a private nested enum (`_interactedBandits`, `:49`).
- The greeting condition is private and sets both text variables itself, so the only seam is a postfix on it.

### Solution Approach

The robbery lines are data only: 64 game-text rows vanilla already looks up. The greetings are a postfix (`Patch122_BanditVoices`, GameInit) over one engine-free choice:

| Piece | Role |
|---|---|
| `taom_bandit_voice_strings.xml` | 64 robbery rows (8 clans, variations `<clanId>_1` and `_2`, four rows each) and 16 greeting rows (`str_taom_bv_greeting.<clanId>_harmless` and `_brothers`). Every row is `{=taom_bv_*}English`, gender-neutral, with the expression tags vanilla's rows use. |
| `BanditVoicesService.Apply` | engine-free. It returns unless the switch is on, this is not a dedicated server, no messenger conversation is open, the party is not at sea, it is a first meeting and the party has a faction; every one of those checks comes before the roll. One roll of `Chance` (0.25, compiled) from `IRandomSource`: below 0.125 "harmless" with vanilla's ultimatum `{=A5IJmN0X}`, below 0.25 "brothers" with `{=ASRpFaGF}`, otherwise nothing. The greeting row check comes after the roll, because the roll picks the row; the row alone decides which factions speak, so a faction without one keeps vanilla's text. |
| `IBanditEncounterTextAdapter` / `BanditEncounterTextAdapter` | the encountered party's `MapFaction.StringId` (the key vanilla builds its own variations from) and whether it is at sea, the first-meeting read, `TrySetGreeting` (false when the row is missing) and `SetUltimatum`. |
| `BanditGreetingPatch` | the postfix on `bandit_neutral_greet_on_condition`. Does nothing unless `__result` is true; a fault is logged once and vanilla's text stands. |
| `BanditVoicesModule` | registrations, the category, `InitializeStatics`. One line in `Main/Composition/FeatureModules.cs`; `IoC.cs` and `SubModule.cs` are untouched. |

**First meeting.** The adapter reads vanilla's `_interactedBandits` through `AccessTools.FieldRefAccess<BanditInteractionsCampaignBehavior, object>`, bound once in the adapter's constructor: the declared type is `Dictionary<MobileParty, PlayerInteraction>` with a private enum, so the reference is typed `object` and read as the non-generic `IDictionary`. A party that is not a key is a first meeting. Vanilla's greeting consequence records the party (`:361-367`). If the field does not bind, the adapter logs one warning and every first-meeting read answers false, so the greetings stay off and vanilla's text stands; the robbery lines are data and keep working. The binding is a row of `reflection-sites.md` and of `ReflectionSiteBindingTests`.

**Ultimatum.** The greeting and the ultimatum are set together, because vanilla's reply must fit the greeting: the harmless claim is answered by `{=A5IJmN0X}I think not, brigand. Surrender or die!` and the hidden-warriors bluff by `{=ASRpFaGF}Nice try, brigand. Surrender or die!`. Both are vanilla keys set verbatim, so vanilla's translations of them apply and TAOM adds no row for them. `BanditVoicesBindingTests` pins that the vanilla method body still holds both strings.

**Faction id.** The key is `MapFaction.StringId` of the encountered party, the one vanilla uses for its own variations. For a bandit party that is its clan.

## Configuration

No config file. MCM "World/Bandit Voices" (`GroupOrder = 78`), "Enable Bandit Clan Greetings" (`EnableBanditGreetings`, `Order = 0`, default on, `RequireRestart = false`). The switch gates the greetings only: the robbery lines are data and always apply. The provider takes the MCM object once through a lazy accessor and reads it live, so a change applies at the next bandit conversation. The chance (0.25) is a compiled constant, not a setting.

| Clan id | Voice |
|---|---|
| `dunland_raiders` | Dunlending hill-folk with an old grievance against the lowlanders and the horse-lords |
| `rhun_raiders` | Easterling riders of the eastern grass, collecting tribute |
| `harad_raiders` | Haradrim desert raiders who sell the road and its water |
| `gundabad_raiders` | orcs of Gundabad, short and brutal |
| `umbar_corsairs` | corsairs ashore, speaking of holds, tides and landing fees |
| `gondor_soldiers` | Gondor deserters, weary and ashamed |
| `erebor_warriors` | Blacklock dwarves, an outcast clan with a ledger of grudges |
| `mirkwood_stalkers` | woodland outlaws whose bows are already drawn |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BanditVoices/BanditVoicesModule.cs` | feature module: registrations, category `Patch122_BanditVoices` at GameInit |
| `Main/Features/BanditVoices/BanditVoicesService.cs` | the choice, `Apply` (a concrete class, nothing fakes it) |
| `Main/Features/BanditVoices/IBanditVoicesSettingsProvider.cs`, `BanditVoicesSettingsProvider.cs` | the MCM switch |
| `Main/Features/BanditVoices/BanditVoicesIoC.cs` | DryIoc registration |
| `Main/Features/BanditVoices/Hooks/BanditGreetingPatch.cs` | the postfix |
| `Main/Adapters/IBanditEncounterTextAdapter.cs`, `BanditEncounterTextAdapter.cs` | the encounter facts, the first-meeting read and the text variables |
| `Main/_Module/ModuleData/taom_bandit_voice_strings.xml` | the 80 rows (registered as a GameText node in `Main/_Module/SubModule.xml`) |
| `Main/Features/TaomSettings.cs` | `EnableBanditGreetings` |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | the setting is classified as presentation |

## Dependencies

- `IRandomSource` (InitialChildGeneration), `IDedicatedServerProvider` (CoopInterop), `IMessengerConversationState` (Messengers), `IModLogger`.
- `TaomSettings` (MCM) for the switch.
- Engine: `BanditInteractionsCampaignBehavior`, `PlayerEncounter`, `MobileParty`, `GameTexts`, `MBTextManager`.

## Localization

The English is in `taom_bandit_voice_strings.xml`; the 12 per-language files (`std_taom_bandit_voice_strings_*.xml`) are seeded with the English default and wired into every `language_data.xml`. The paid translation run is not done (it goes into #763). Until it runs, non-English players see English in about half of these clans' robberies, and a shown greeting is English while its ultimatum (a vanilla key) is translated. The table row is in `tools/_loc_sources.py`, and `tools/generate_translation_template.py` lists the file as not templated.

## Co-op

No gate. The postfix writes two local conversation text variables and skips nothing; the module declares no campaign behavior. It stands down on a dedicated server and during a messenger conversation, and the roll uses TAOM's own random source, never the engine's shared stream. The setting is classified as presentation in `CoopSettingsRelevance`. See [coop-interop.md](coop-interop.md). No co-op session has run this build.

## Tests

- `TAOM.Tests/Features/BanditVoices/BanditVoicesServiceTests.cs`: every gate except the row check writes nothing before the dice are touched (switch, sea, repeat meeting even on a low roll, dedicated server, messenger, no faction); the row check comes after the roll, so a missing row writes no ultimatum; the roll boundaries (below 0.125, up to 0.25, at and above 0.25), the paired vanilla ultimatum, and the one variation asked about.
- `TAOM.Tests/Features/BanditVoices/BanditVoicesDataTests.cs`: every robbery variant has all four rows under one variation id, no variation beyond the two per clan, both greetings per clan, nothing for spiders, trolls or looters, one dot per id, each key `taom_bv_*` and used once, none a vanilla key or id (`LiveInstall`), no other strings file uses a `taom_bv_` key, no gendered word, no line that names the listener's people (`NoLine_NamesTheListenersPeople`), no placeholder, no dash, only the expression tags vanilla's rows use.
- `TAOM.Tests/Features/BanditVoices/BanditVoicesBindingTests.cs` (`BindingVerification`, `RequiresGame`): the target is the private parameterless bool condition, the postfix's parameter name, the method body still writing both variables with vanilla's two ultimatum texts, `_interactedBandits` binding as an `IDictionary` keyed by the party, the first-meeting rule, one postfix attached and the JIT accepting it.
- `TAOM.Tests/Features/BanditVoices/BanditVoicesPatchTests.cs`: the postfix does nothing when vanilla's condition is false, reaches the service when it is true, logs one warning for a throwing service or adapter, and the SubModule.xml node loads the strings file for Campaign and CampaignStoryMode only.
- `TAOM.Tests/Features/BanditVoices/BanditVoicesWiringTests.cs`: module listed once, category and phase unique, services resolve, `IoC.cs` and `SubModule.cs` untouched.
- `TAOM.Tests/Features/BanditVoices/BanditVoicesSettingsProviderTests.cs`: default on, the MCM attributes, the co-op classification.
- Binding gate: `HarmonyPatchBindingTests`, `ReflectionSiteBindingTests`, `PatchCategoryNumberTests`, the regenerated `taleworlds-api-snapshot/patch-targets.md`.

## How to Retune

- **A line:** edit the English in `taom_bandit_voice_strings.xml`. A changed English line stales its 12 translations (`tools/translation_overrides/<lang>.json` wins over the cache).
- **A third robbery variation for a clan:** vanilla reads k up to 6, so add all four rows `str_robbery_threat`, `str_robbery_pay_agreement`, `str_robbery_conclusion` and `str_robbery_start_fight` under `<clanId>_3`, and extend the data test.
- **The greeting chance:** `BanditVoicesService.Chance`. A compiled constant by decision.
- **A new bandit clan:** add its eight robbery rows and two greeting rows, and the data test's list. The rows alone decide who greets.

## Engine bump

Re-check against the previous version: that `bandit_neutral_greet_on_condition` still exists with no parameters and still writes `BANDIT_NEUTRAL_GREETING` and `BANDIT_ULTIMATUM`; that its harmless and hidden-warriors branches still use `{=A5IJmN0X}` and `{=ASRpFaGF}`; that `_interactedBandits` is still a dictionary keyed by the party; that `bandit_start_defender_condition` still builds variations from `MapFaction.StringId + "_" + k` and still asks for all four lines under one variation.

## Changelog

- 2026-10-26: first version (#900).

## How to verify in game

Owed; none of these has been run. No console command spawns a bandit party in front of the player, so these start on the map.

**Preconditions:** a new or existing campaign, TAOM only, MCM "World/Bandit Voices" on, no restart needed.

1. Let a Dunlending, Gundabad or Umbar bandit party attack the player's party on land, then choose "Maybe we can work out something." About half the robberies use the clan's own set; repeat until one does, and check that its lines come from one set with no "ERROR: Text with id" line.
2. Attack a bandit party of one of the eight clans on land and open the conversation. About one conversation in four should open with the clan's own greeting and the matching vanilla reply ("I think not, brigand." or "Nice try, brigand."). Run it several times; a party met before should never greet that way again.
3. Switch "Enable Bandit Clan Greetings" off: every greeting is vanilla's. The robbery lines in step 1 stay the clan's.
4. A Spider of Mirkwood brood and a wild troll band skip the conversation as before; looters keep vanilla's lines.
5. Check the logs for `[BanditVoices]` lines (none expected).

## GitHub Issue

- **Issue:** #900, bandit clan voices
- **Status:** Open until the in-game checks are done.
