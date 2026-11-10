# Sworn Anew

## Overview

A captured lord can be asked to join the player's kingdom. Vanilla hides the persuasion for a prisoner in the diplomacy menu (its defection model ends with "not a prisoner"), and only a kingdom's ruler can recruit a captured lord, through a separate line, the ruler's prisoner line, "join us, and be set free", which checks no realm. Sworn Anew lets the player ask: the lord then serves the player's kingdom, and the player gains a vassal instead of a ransom. A lord of the Free Peoples is never asked to join an Evil realm, nor an Evil lord a Free one, and Sauron and the Nine are never asked. One switch in MCM, "World/Sworn Anew" (default on). When it is off, the base game's rules apply: only a kingdom's ruler can recruit a captured lord, through its "join us, and be set free" line, which checks no realm. When it is on, both lines pass TAOM's check, and the ruler-only line also needs the lord held by the player's party or in the player's clan's dungeon.

## Why This Exists

- **Vanilla behavior:** `DefaultDefectionModel.CanHeroDefectToFaction` checks the faction terms and ends `return !hero.IsPrisoner`, so the two diplomacy-menu options ("Your liege is not worth your loyalty", "Candidly, what do you think of your liege") never appear for a prisoner. Vanilla's separate line, the ruler's prisoner line, "I have an offer for you: join us, and be set free", does not ask the model at all, and offers itself to any prisoner who passes its own conditions, whatever the realm. It shows from the party screen and in a player-clan dungeon.
- **TAOM requirement:** turning a captured lord is plausible for a Dunlending or an Easterling, and the issue's binding decision is that a Gondor lord defecting to Mordor would break lore, so both entry points need the same alignment gate, and the lore heroes are excluded.
- **Without this feature:** only a kingdom's ruler can recruit a captured lord, through the ruler's prisoner line, and that line checks no realm, so a Mordor ruler can be offered a Gondor lord. Any other captured lord is a ransom and nothing more. With the diplomacy option opened and the line left alone, the two entry points would disagree.

## Architecture

### Design Challenge

- `DefectionModel` has one caller, `LordDefectionCampaignBehavior`: the two diplomacy-menu conditions (`conversation_player_is_asking_to_recruit_enemy_on_condition`, `conversation_player_is_asking_to_recruit_neutral_on_condition`, `LordDefectionCampaignBehavior.cs:781-803`, registered at `:137-138`). The menu is reached by a prisoner through "There is something I'd like to discuss" (`LordConversationsCampaignBehavior.cs:811`, whose condition `conversation_hero_main_options_discussions` has no prisoner check). The model has no AI-side use. No installed module ships a defection model.
- The prisoner line `player_prisoner_talk` (`:141`; shown in a conversation with a prisoner of the player's party and with one in a dungeon of the player's clan) has its own condition, `conversation_player_start_defection_with_prisoner_on_condition` (`:369-384`): the player leads a kingdom, the lord leads their own clan, is a prisoner, is not of the ruling clan, has no blood feud, and is held by the main party or in a settlement the player's clan owns. It never consults the model, so it needs its own seam.
- A defection ends with `ChangeKingdomAction.ApplyByJoinToKingdomByDefection` (`JoinKingdomAsClanBarterable.Apply`). `PrisonerReleaseCampaignBehavior.ClanChangedKingdom` then releases the lord for their new kingdom (`EndCaptivityAction.ApplyByPeace`), from the prison roster of the main party, a kingdom dungeon or a war party. A conversation opened from the party screen leaves the screen's snapshot alive, and leaving the screen with Escape refills the prison roster from it (`PartyScreenData.ResetUsing`), putting the released lord back. The precedent fix is `HardQuestionsActionAdapter.CommitOpenPartyScreen` (`PartyScreenLogic.DoneLogic(true)`), now one shared helper, `PartyScreenCommit.CommitIfMainParty`, which commits a screen whose right owner is the main party. The commit protects the item roster as well as the prisoners (gifts, the strip, a defection). One case is skipped: the ransom screen with prisoners on the broker's side, which binds the live item roster but cloned troop rosters, and whose Done handler sells the broker's side again. For the same reason Sworn Anew refuses while the open party screen is a ransom screen (`DefectionFacts.OnRansomScreen`): its cloned prison roster could still ransom a lord who just joined.
- `MbEvent` runs the newest listener first, and TAOM's behaviors register after SandBox's, so a TAOM `OnClanChangedKingdomEvent` listener runs before vanilla's release (`harmony-il.md`, "Campaign-event listener dispatch is LIFO"). The commit therefore cannot happen inside the event; the behavior queues it on the conversation manager's `ConversationEndOneShot`, which fires when the conversation ends.

### Solution Approach

Three thin entry points over one service, `SwornAnewService`, which is pure over the raw facts `ISwornAnewAdapter` reads.

| Entry point | What it does |
|---|---|
| `TaomDefectionModel : DefaultDefectionModel` (campaign only, `GameModelDecl`) | `CanHeroDefectToFaction` only: `_service.CanPrisonerDefect(_adapter.Read(hero, kingdom)) ?? base.CanHeroDefectToFaction(hero, kingdom)`. Opens the diplomacy option for a prisoner the service allows. |
| `Patch116_PrisonerDefectionLine_Postfix` (postfix on the private prisoner-line condition, category `Patch116_SwornAnew`, ProcessLoad) | When vanilla offers the line, takes it away if `HidesPrisonerLine` says the service refuses the prisoner: the full verdict, on every peer, because it only hides a line locally. |
| `SwornAnewBehavior` (campaign behavior, built with `new` per campaign) | On an `OnClanChangedKingdomEvent` defection of the conversation partner's clan into the player's kingdom, asks the adapter to commit the open party screen when the conversation ends (`ConversationEndOneShot`). Holds no state and saves nothing. |

**The rules** (all in `SwornAnewService`: `CanPrisonerDefect` answers the model, `HidesPrisonerLine` the postfix, both over the same private `Allows`):

| Case | Verdict |
|---|---|
| No facts, or the hero is not a prisoner | null (vanilla's own check decides: false for a prisoner); the postfix hides nothing |
| Dedicated server, or a co-op peer deferring to the host | null for the model; the postfix still applies the verdict, since it only hides a line on that machine |
| Switch off | null; the postfix hides nothing |
| The open party screen is a ransom screen (`OnRansomScreen`) | false (its cloned prison roster could still ransom a lord who just joined) |
| Not in a kingdom, already in the player's faction, the ruler of their kingdom, no clan, a minor faction, under mercenary service, or their clan has no kingdom | false (vanilla's own faction terms) |
| Blood feud with the player, and the player's clan rules the target kingdom | false (vanilla applies the feud only to a kingdom the player's clan rules) |
| Not held by the main party and not in a settlement the player's clan owns | false (custody, as the vanilla prisoner line) |
| `IUncapturableRegistry.IsUncapturable(heroId, raceId)` | false (Sauron and the Nine, by identity, whatever the Uncapturable Heroes switch says) |
| Free lord and Evil player realm, or Evil lord and Free player realm | false |
| Anything else, a Neutral side on either end included | true |

- **Sides** come only from `FactionSides` (`OfOwnClan(hero)` for the lord, `OfPlayer()` for the player), each the id and culture of one entity. The lord is sided by their own clan, never by the realm they serve, as Kingdom Allegiance sides lords (#1114): an orc lord whose clan serves Gondor reads Evil, a Free lord serving a Neutral realm reads Free, and a Neutral lord (a Khand clan, say) serving Mordor reads Neutral, so any player may now ask them. The player is sided by the player's realm. Each pair is resolved with `IAlignmentService.ResolveSide(SideIds)`. The Free-against-Evil test is `FactionSideRules.AreOpposed` (Main/Features/Execution, beside `FactionSide`), shared with gifts, hires, tournaments and battle thanks; `AreEnemyAlignments` is never used, because it calls Neutral the enemy of everyone.
- **Lore** reuses the uncapturable registry; there is no new list and no new JSON. A hero the registry's exclude list hands back to vanilla capture is therefore not lore-locked here either.
- **A player in mercenary service** gets the same answer as vanilla: no extra check.
- **After the lord joins:** the engine releases them for the new kingdom. Good Deeds rewards only `ReleasedByChoice`, so there is no second reward, and Hard Questions clears its state on a release as for any other.

### Component Diagram

```
conversation (diplomacy option)          conversation (ruler's prisoner line)
          |                                          |
 TaomDefectionModel                    Patch116 postfix (AND of the verdict)
          \                                         /
           ------------ SwornAnewService ----------
          (CanPrisonerDefect)   (HidesPrisonerLine)
                    |            |          |
         ISwornAnewAdapter  IAlignmentService  IUncapturableRegistry
         (engine facts)

OnClanChangedKingdomEvent -> SwornAnewBehavior -> ISwornAnewAdapter.CommitOpenPartyScreenWhenConversationEnds
    -> ConversationEndOneShot -> PartyScreenCommit.CommitIfMainParty
```

## Configuration

MCM, "World/Sworn Anew", `GroupOrder = 71`.

| Setting | Default | Notes |
|---|---|---|
| Enable Sworn Anew | on | Read at each conversation question; no restart. The compiled default lives in `SwornAnewSettingsProvider.DefaultEnabled`, shared by the property initializer and the fallback before MCM is up. A new setting, so the default reaches every install. |

With the switch off, the model answers vanilla for every hero (a prisoner never defects) and the postfix leaves vanilla's prisoner line alone. No localization: the options use vanilla's dialog text, and a refused option simply does not appear.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SwornAnew/SwornAnewModule.cs` | Feature module: services, the model, the behavior, the Patch116 category and its statics handshake |
| `Main/Features/SwornAnew/SwornAnewService.cs` | The verdict and the commit decision |
| `Main/Features/SwornAnew/SwornAnewSettingsProvider.cs`, `ISwornAnewSettingsProvider.cs` | The one MCM switch |
| `Main/Features/SwornAnew/Models/DefectionFacts.cs` | The raw facts one question rests on |
| `Main/Features/SwornAnew/Models/TaomDefectionModel.cs` | The model override |
| `Main/Features/SwornAnew/Hooks/Patch116_PrisonerDefectionLine_Postfix.cs` | The ruler's prisoner line seam |
| `Main/Features/SwornAnew/Hooks/SwornAnewBehavior.cs` | Queues the party-screen commit for the end of the conversation |
| `Main/Adapters/ISwornAnewAdapter.cs`, `SwornAnewAdapter.cs` | Engine facts, the conversation partner, the queued commit |
| `Main/Adapters/PartyScreenCommit.cs` | The shared commit of the open party screen (also used by Hard Questions and Tokens of Esteem) |
| `Main/Features/Execution/FactionSideRules.cs` | `AreOpposed`, the Free-against-Evil test |
| `Main/Features/TaomSettings.cs` | `SwornAnewEnabled` |

## Dependencies

- `IUncapturableRegistry` (UncapturableHeroes, registered in `UncapturableHeroesIoC`): who can never be taken, hence never asked.
- `IAlignmentService` (Execution): the side of the lord's own clan and of the player's realm.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop and the kernel): the stand-down gate.
- `FactionSides` (Adapters): the only place a `SideIds` pair is built.

## Tests

- `SwornAnewServiceTests`: every row of the rules table, one test per skip guard (the ransom screen, each vanilla faction term, the blood feud with and without the player ruling, both custody sources and neither, a lore hero, Free against Evil in both orders, Free with Free, Evil with Evil, a Neutral side on either end), the id and race the registry is asked about, each side resolved from its own pair, `HidesPrisonerLine` (including a deferring co-op peer and a dedicated server) and the commit decision.
- `TaomDefectionModelTests` (needs the game): a prisoner's verdict is the answer, a free hero falls through to the base, the model overrides one method over `DefaultDefectionModel` and its body holds no decision.
- `Patch116PrisonerDefectionLineTests`: vanilla's answer is the ceiling, the verdict can only take the line away, a free hero, the switch off and an unreadable partner leave it, a fault keeps it and is logged.
- `SwornAnewBindingTests` (engine binding gate): the private condition's shape, the postfix parameter name, the `player_prisoner_talk` line still registered with this condition, the category applying and the JIT accepting it, both diplomacy conditions still asking the model, the slot's generic argument.
- `SwornAnewBehaviorTests` (needs the game): a defection of the partner's clan queues the commit once, every other kingdom change asks the adapter nothing, the switch, the clan check and faults.
- `PartyScreenCommitTests`: only the shared helper forces a party screen done, each adapter commits through it, and the helper checks the screen's right owner is the main party and skips the ransom case before it forces Done.
- `SwornAnewSettingsProviderTests`: the default, the fallback before MCM, live reads, the group name and order.
- `SwornAnewWiringTests`: the module listed once, the single-owner files untouched, one model, one patch, one behavior built per campaign, and the postfix vetoing a refused prisoner through the container's registrations, and the adapter siding the lord by `OfOwnClan` (`FactionSidesTests` pins that `OfOwnClan` never reads the realm). The one-filler-per-slot scan is the generic `FeatureModulesTests.EveryDeclaredGameModel_IsTheOnlyTaomFileFillingItsSlot`.
- `FactionSideRulesTests`: all nine pairs. `CampaignHotPathSettingsProvidersTests` lists the settings provider.
- `SettingsFingerprintTests` and `ServerSettingsSchemaTests` carry the new setting in their counts.
- The behavior has its co-op row in [coop-interop.md](coop-interop.md).

## How to Change the Rules

1. The switch: MCM. No code change.
2. A new refusal (another lore group, another side rule): add it to `SwornAnewService.Allows` after the custody term, with a test per case. Both entry points pick it up, because the model and the postfix ask the same private method.
3. A new default: change `SwornAnewSettingsProvider.DefaultEnabled` and the test that pins it. A persisted MCM value keeps the old default: rename the setting, and say why in a comment.

## Known Limits

- **Unverified in game:** whether the persuasion flow works with a prisoner end to end, where the lord ends up after they agree, and who frees them. The code follows vanilla's own path (`JoinKingdomAsClanBarterable`, then `PrisonerReleaseCampaignBehavior`) and has not been run. The checklist below is the first thing to do.
- **The postfix also hides vanilla's prisoner line in two cases vanilla allows:** a prisoner whose clan is under mercenary service, and a prisoner already in the player's own faction. The full verdict is applied, not the alignment alone: a mercenary clan's lord who joined through the line would raise `JoinAsMercenary` and never arm the party-screen commit. Vanilla's other terms for the line are unchanged.
- **A switch-off restores vanilla's prisoner line untouched.** With the feature off the postfix hides nothing, so the line is offered to any prisoner vanilla offers it to, whatever the realm. The diplomacy option stays hidden for a prisoner.
- **The commit skips the ransom screen with prisoners on the broker's side.** That screen binds cloned troop rosters and its Done handler sells the broker's side again, so it is left as it was; its prisoners are sold once when the player leaves with Escape. Every other screen whose right owner is the main party is committed, which keeps the item roster.
- **On the ransom screen the offer is not made.** Hard Questions offers no lines on the ransom screen since #1001.
- **Lore is the uncapturable registry's, by design.** Sauron and the Nine are covered; a lord a future list adds there is covered too; a lord who is not on it but belongs to a lore group is not.
- **Co-op:** a peer that defers to the host, and a dedicated server, get vanilla's behavior from the model and the commit; the postfix's veto of the prisoner line still runs on every peer, because it hides a line only on that machine. BannerlordCoop does not disable `LordDefectionCampaignBehavior` (its `LordBarterPatch` routes the defection barter to the server), so the host's flow is the single-player flow; that has not been exercised in a session.
- **A clan the game creates during play takes its settlement's culture.** A companion granted a fief (`Clan.CreateCompanionToLordClan`) and a town's rebels (`Clan.CreateSettlementRebelClan`) get a new clan with that settlement's culture, so its lord is sided by the settlement's people. It matters only once such a clan has left the player's realm for another and its lord is in the player's custody. Kingdom Allegiance reads these clans the same way.
- **Persuasion can fail.** The option opens vanilla's persuasion, so a lord can refuse, and vanilla's cooldown on a refused attempt applies.

## In-game Checklist

Setup: a campaign with cheat mode on; `campaign.declare_war` to put the player at war with a kingdom, and a way to hold a lord (capture one in battle, or add one with the console).

1. **Diplomacy option, same side.** As a Free player (a Gondor, Rohan or Rivendell realm), hold a Free lord of another kingdom prisoner in the main party and talk to them (the prisoner conversation, then "There is something I'd like to discuss"). Expect the "Your liege is not worth your loyalty" option, or its neutral twin if the kingdoms are not at war.
2. **Where the lord ends up.** Persuade them. Note where they are afterwards (the player's kingdom, free or still a prisoner), which party they join, and whether the prison roster is empty of them.
3. **Party screen, then Escape.** Open the party screen, talk to the prisoner from it, persuade them, end the conversation, then close the party screen with Escape. With the commit, Escape closes the screen with no "cancel changes" prompt, and the lord stays released and in the kingdom; if they return to the prison roster, or the prompt appears, the deferred commit failed.
4. **Dunlending and Easterling.** Capture a Dunlending lord and an Easterling lord and ask both. As a Free player, both should be refused (the option absent) in the diplomacy menu and on the ruler's prisoner line. As a Mordor or Isengard player, both should be offered.
5. **Gondor lord, Mordor player.** Play a Mordor ruler, capture a Gondor lord: no diplomacy option and no ruler's prisoner line. Then play a Gondor ruler and capture a Mordor lord: the same.
6. **Neutral.** Capture a lord of a Shaghana or Abanissa clan, whatever realm they serve (Neutral; Harad, the `aserai` culture, is Evil) as a Free player and as an Evil player: offered in both.
7. **Lore heroes.** As an Evil player, capture a Nazgul who leads a clan and is not a ruler (the Uncapturable Heroes switch off, so the Nine can be captured): no option, no line. Sauron is refused earlier, as the ruler of his kingdom.
8. **Custody.** Hold a lord in a dungeon of a settlement your clan owns, and talk to them there: offered. A lord held by another clan's party or settlement: not offered.
9. **Vassal and ruler.** Ask as a vassal of a kingdom (the diplomacy option) and as the ruler (both options, plus the ruler's prisoner line, which vanilla requires a ruler for).
10. **Lord's own state.** A prisoner who is the ruler of their kingdom, a minor-faction lord or a mercenary lord: not offered in the diplomacy menu.
11. **Switch off.** Turn "Enable Sworn Anew" off: the diplomacy option disappears for a prisoner; the vanilla ruler's prisoner line behaves as in the base game. Turn it on again mid-campaign: no restart.
12. **No double reward.** Check that Good Deeds adds nothing for the release that follows a defection (it pays only a release by choice).
13. **Second campaign.** Start a new campaign in the same session and repeat step 3: the commit still happens once, with nothing carried over from the first.
14. **Co-op host.** If a session is available: the host offers the option; a client does not.
15. **Ransom broker screen.** Hold two prisoners besides the lord and open the ransom broker's sell-prisoners screen. If the screen allows a conversation, talk to the lord from it: neither the diplomacy option nor the ruler's prisoner line is offered. Leave the screen with Escape: the other prisoners are sold once, not twice (check the gold), and the screen is not committed.
16. **Ransom screen with nothing for sale, and the alley screen.** Open each, talk to the lord from it: the offer is still absent. Then use a gift from the same screen (give an item), move one troop and press Escape: the item stays gone.
17. **A lord whose own people differ from their realm.** In a fresh campaign, give a Gondor clan an Evil culture with `campaign.set_clan_culture [ClanName] | mordor` (the cheat takes a clan or culture id, or its name), which stands for an orc clan serving Gondor. Capture its lord as a Rohan or other Free player: not offered. Capture them as a Mordor or Isengard player: offered. Then give a Mordor clan a Neutral culture (Khand's, `battania`) and capture its lord as a Free player: offered.

## Changelog

- 2026-10-23: first version (#869). The defection model, the prisoner line postfix (Patch116) and the deferred party-screen commit, behind one MCM switch at `GroupOrder = 71`.
- 2026-11-10 (#1114): the lord is sided by their own clan (`FactionSides.OfOwnClan`), not their realm, the same as Kingdom Allegiance (#1111).

## GitHub Issue

- **Issue:** #869, [Captured lords can be asked to join the player's kingdom](https://github.com/haterade22/TAOM/issues/869)
- **Status:** Shipped on `bannerlord-1.5.x` in `da6af0c0b` (v2.0.35). Closed with `triage-needs-ingame`: the in-game checks are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/gamemodel-registry.md](../reference/gamemodel-registry.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
