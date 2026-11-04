# Alignment-Aware Execution System

> **Engine status (2026-09-14, Bannerlord v1.5.2).** Everything from the second H2 down describes the
> v1.4.8 engine and is kept as the record of why the feature took the shape it did. v1.5.0 deleted
> `ExecutionRelationModel` engine-wide and replaced the argument-less
> `TraitLevelingHelper.OnLordExecuted()` with `OnBloodFeudStarted(Hero executedHero)`, so
> `TaomExecutionRelationModel`, `ExecutionContext` and the `KillCharacterAction.ApplyInternal` /
> `OnLordExecuted` patches no longer exist. The live wiring is in [`execution.md`](execution.md).

## The v1.5.x seam (current)

An execution is now blood-feud fallout, all of it inside `ExecutionCampaignBehavior` (v1.5.2 decompile,
`E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs`):

1. `KillCharacterAction.ApplyInternal` still destroys a leaderless victim clan
   (`DestroyClanAction.ApplyByClanLeaderDeath`, which nulls its kingdom) BEFORE dispatching `OnHeroKilled`.
2. `OnHeroKilled(victim, killer, Executed)` branches: `killer == Hero.MainHero` goes to
   `OnPlayerExecutedHero(victim)`; `victim.Clan == Clan.PlayerClan` goes to
   `OnPlayerClanMemberExecuted(victim, killer)`. The deferred `ExecutionAfterMapEvent` path reaches
   `OnPlayerExecutedHero` from `OnDeathMarkAdded` with the same `killer == Hero.MainHero` gate.
3. `OnPlayerExecutedHero`: if the victim's clan has no feud with the player yet,
   `ChangeBloodFeudStateAction.StartBloodFeudWithClanByPlayerExecutingAHero(victim.Clan, victim)` and then
   `TraitLevelingHelper.OnBloodFeudStarted(victim)`: Honor -1000 for a victim with Honor at or above 0,
   otherwise Mercy -500, plus Mercy -500 unconditionally. **TAOM prefix:**
   `TraitLevelingHelper_OnBloodFeudStarted_Patch` returns `ShouldApplyHonorPenalty(victim, executor)`;
   `false` skips all of it. The victim's kingdom can be null here (step 1), hence the culture fallback.
4. The feud action dispatches `OnBloodFeudStateChanged(clanWithFeud, executedHero, detail)`: the relation
   between the player's leader and the feud clan's leader is set to the floor, then, only if
   `clanWithFeud.Kingdom != null`, every clan but the player's is asked
   `GetBloodFeudStartRelationPenaltyToOtherClan(executedHero, item)` and any non-zero answer is applied to
   the PLAYER's relation with that clan (`showQuickNotification: false`) and counted for one summary
   notice. Vanilla answers -45 (honourable noble victim) or -30 for a clan on good terms with the
   victim's clan, -50 or -25 for a clan in the victim's kingdom, else 0. **TAOM postfix:**
   `ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch` rewrites that int through
   `IExecutionRelationService`; zero means the clan is skipped and not counted.
5. The same static method feeds `HeroExecutionSceneNotificationData.GetExecuteTroopHintText`, the
   pre-execution tooltip, so the warning and the outcome agree per clan.
6. `OnPlayerClanMemberExecuted` (an AI executor, a player-clan victim) starts the feud the other way round
   (`StartBloodFeudWithClanByAIExecutingPlayerRelative`) and runs the SAME loop against the player's
   relations. The signature carries no executor and the player is the bereaved, so the postfix leaves
   vanilla's number alone there (`IOnExecutionAction.IsPlayerTheBereaved`). Found by the 2026-09-14
   deep review of the v1.5.2 port; the first cut assumed the player was the executor on every path.

Each participant is a `SideIds` pair built by `FactionSides` (#957) at the patch: the executor from `OfPlayer()` (the
player clan's map faction with that faction's culture), the victim from `OfHero(dyingHero)`, the evaluator from
`Of(otherClan.MapFaction)`. The earlier build zipped each hero's kingdom with the hero's own culture, which sided a
lord of a Neutral realm by a culture the realm does not have. `IExecutionRelationService` and `IOnExecutionAction` take
the `SideIds` directly; the `ExecutionParticipant` struct they once took is gone. `FactionId` carries the map faction
id: a kingdom, or a clan serving none.

## Overview

The Alignment-Aware Execution system replaces vanilla Bannerlord's one-size-fits-all lord execution penalties with a LOTR-thematic system that considers the moral alignment of the executor, victim, and every evaluating clan leader. When a Free Peoples lord executes a servant of Sauron, there is no dishonor — only justice. When a lord slays one of their own allies, it is kinslaying, punished far more harshly than vanilla.

## Why This Exists

Vanilla Bannerlord applies the same massive penalties to every execution regardless of context:
- **-1000 Honor XP** to the player's Honor trait
- **-60 relation** with the victim's clan
- **-30 relation** with friends of each evaluating clan leader
- **-10 relation** with all same-faction lords and all honorable nobles worldwide

This breaks LOTR immersion. Aragorn executing the Mouth of Sauron shouldn't make him dishonorable. Theoden executing a captured Uruk-hai warlord shouldn't turn Gondor against him. Meanwhile, Denethor executing a Rohan lord should be considered kinslaying — a far graver act than the vanilla system recognizes.

### Design Decisions

These decisions were explicitly confirmed during design:

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Cross-alignment execution penalties | **Zero** for allies of executor | Free/evil factions approve of killing their enemies |
| Neutral kingdoms | **Treated as enemy by both sides** | Umbar, mercenary factions — everyone can execute them freely |
| Same-alignment execution | **50% harsher than vanilla (kinslaying)** | Killing your own side is treachery in Middle-earth |
| AI execution behavior | **Player only** | No AI execution logic; vanilla AI already never executes |

## Vanilla Execution Flow (What We Override)

Understanding the vanilla flow is essential to understanding what this feature changes. This was first determined by decompiling the Bannerlord v1.3.12 DLLs, and re-verified against v1.4.8 on 2026-09-06 while fixing #556. Line numbers below are v1.4.8.

### Entry Points

There are two ways a player triggers an execution:

**1. Party Screen** — `PartyCharacterVM.ExecuteExecuteTroop()` in `TaleWorlds.CampaignSystem.ViewModelCollection.Party`. The player right-clicks a prisoner hero in their party and selects Execute. Eligibility is checked by `PartyScreenLogic.IsExecutable()`:
- Must be `TroopType.Prisoner` in the player's party roster
- Must be a named Hero (not a regular troop)
- Must be of age (`AgeModel.HeroComesOfAge`)
- Must not be during an active `PlayerEncounter`
- Must pass visual maturity check (not Tween or younger)

**2. Post-Battle Conversation** — `LordConversationsCampaignBehavior` offers an execution option during the post-defeat lord conversation dialogue, which triggers the same execution scene.

### Execution Scene

`HeroExecutionSceneNotificationData` plays a cinematic scene (`scn_execution_notification`) where the victim is shown in battle armor (weapons stripped) and the executor holds an `execution_axe` item. Before confirmation, the UI previews relation changes by iterating ALL clan leaders and calling `ExecutionRelationModel.GetRelationChangeForExecutingHero()`.

### Core Kill Chain — `KillCharacterAction.ApplyInternal()`

This is the central method in `TaleWorlds.CampaignSystem.Actions.KillCharacterAction`. It is a `private static` method on a `public static class`. The two execution entry points both call it:
- `ApplyByExecution(victim, executer)` — detail = `KillCharacterActionDetail.Executed` (value 6)
- `ApplyByExecutionAfterMapEvent(victim, executer)` — detail = `KillCharacterActionDetail.ExecutionAfterMapEvent` (value 7)

**Step-by-step flow of `ApplyInternal`:**

1. **CanDie check** — `Hero.CanDie()` fires `CanHeroDie` event, letting active quests block the death. The party screen path calls with `isForced: false`; the conversation path with `isForced: true`.

2. **Deferred death** — If the victim is in an active map event or siege, death is deferred via `Hero.AddDeathMark()`. The actual kill runs later on `AgingCampaignBehavior`'s daily tick.

3. **Pre-kill event** — `OnBeforeHeroKilled` fires. `CommentOnCharacterKilledBehavior` creates a `CharacterKilledLogEntry` and `DeathMapNotification`.

4. **Obituary** — Encyclopedia text: *"{S/He} was executed in {YEAR} at the age of {AGE}."*

5. **Clan leader succession** — If the victim was clan leader, `ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader()` selects an heir via `HeirSelectionCalculationModel`. The new leader **inherits 70% of the dead leader's relations** with all alive heroes. If the victim was also kingdom ruler, a `KingSelectionKingdomDecision` vote is triggered (or the kingdom is destroyed if no eligible clans remain).

6. **Army disbandment** — If the victim was army leader, the army is disbanded.

7. **MakeDead** — State = `Dead`, death day recorded. If the victim was a prisoner, `EndCaptivityAction.ApplyByDeath()`. If they led a party, leadership is transferred or the party is disbanded.

8. **Governor removal** — `ChangeGovernorAction.RemoveGovernorOf()`.

9. **Honor trait penalty** — **THIS IS WHAT WE PATCH.** The vanilla code:
   ```
   if (detail is Executed or ExecutionAfterMapEvent
       && killer == Hero.MainHero
       && victim.Clan != null
       && victim.GetTraitLevel(DefaultTraits.Honor) >= 0)
   {
       TraitLevelingHelper.OnLordExecuted();  // -1000 Honor XP
   }
   ```
   `TraitLevelingHelper.OnLordExecuted()` is a `public static` method on a `public static class` with **no parameters** — it always applies -1000 XP to `Hero.MainHero`. It has a likely vanilla bug: it uses `ActionNotes.SacrificedTroops` instead of a dedicated execution note.

10. **Clan destruction** — If the victim was clan leader and no successor was found, the entire clan is eliminated (all remaining heroes killed, all fiefs redistributed, removed from all wars).

11. **Post-kill event** — `OnHeroKilled` fires, which triggers `CharacterRelationCampaignBehavior` to apply relation changes.

> **Steps 10 and 11 are in that order (L133-143 then L144), and it bites.** `DestroyClanAction.ApplyInternal` calls
> `ChangeKingdomAction.ApplyByLeaveKingdomByClanDestruction`, which nulls `Clan.Kingdom`. So by the
> time the relation pass runs, an executed lord who was the last adult of his clan has **no kingdom
> left to read**. Anything that re-derives the victim's identity during step 11 gets `null`. This is
> what the v1.4.8 `ExecutionContext` snapshot existed to defeat (it recorded victim and executor at the top of
> `ApplyInternal`, before either step could run); today `FactionSides.OfHero` gives a kingdom-less victim his own
> clan's pair. The tell that this is happening in a live game is that
> the pre-confirm preview shows one set of numbers and the applied result is different, because the
> preview runs the same model before the kill. Cost the project #556.

12. **Cleanup** — Spouse nulled (widowed), companion removed, removed from location.

### Vanilla Relation Change Model — `DefaultExecutionRelationModel`

**Class:** `TaleWorlds.CampaignSystem.GameComponents.DefaultExecutionRelationModel` (NOT sealed, inherits from `ExecutionRelationModel`)

The abstract base class `ExecutionRelationModel` (in `TaleWorlds.CampaignSystem.ComponentInterfaces`) defines 10 abstract `int` properties and 1 abstract method:

```
Properties (all abstract int):
  PlayerExecutingHeroClanRelationPenalty           = -60
  PlayerExecutingHeroFriendRelationPenalty          = -30
  PlayerExecutingHeroFactionRelationPenalty         = -10
  PlayerExecutingHeroHonorableNobleRelationPenalty  = -10
  PlayerExecutingHeroClanRelationPenaltyDishonorable    = -30
  PlayerExecutingHeroFriendRelationPenaltyDishonorable  = -15
  PlayerExecutingHeroFactionRelationPenaltyDishonorable = -5
  PlayerExecutingHeroHonorPenalty                       = -1000
  HeroKillingHeroClanRelationPenalty                    = -40
  HeroKillingHeroFriendRelationPenalty                  = -10

Method:
  abstract int GetRelationChangeForExecutingHero(Hero victim, Hero hero, out bool showQuickNotification)
```

`DefaultExecutionRelationModel.GetRelationChangeForExecutingHero()` evaluates each clan leader with this priority chain (first match wins):

**If victim has Honor < 0 (dishonorable):**

| Evaluator relationship | Penalty |
|----------------------|---------|
| Same clan as victim | -30 |
| Friend of evaluating leader | -15 |
| Same faction lord | -5 |

**If victim has Honor >= 0 (honorable/neutral):**

| Evaluator relationship | Penalty |
|----------------------|---------|
| Same clan as victim | -60 |
| Friend of evaluating leader | -30 |
| Same faction lord | -10 |
| Honorable noble (Honor > 0, not rebel clan) | -10 |

**Important quirk:** The method reads penalty values from `Campaign.Current.Models.ExecutionRelationModel.*` (i.e., via the GameModel accessor, resolving to the currently registered model), not from `this.*`. This means if you register a custom model, your overridden property values will be used even when `base.GetRelationChangeForExecutingHero()` is called.

The relation changes are applied by `CharacterRelationCampaignBehavior.OnHeroKilled()` — it only fires for player executions, iterates ALL non-eliminated non-bandit clan leaders, calls the model, and applies changes via `ChangeRelationAction.ApplyPlayerRelation(leader, change, affectRelatives: true)`.

### AI Execution Behavior

**AI lords NEVER proactively execute prisoners** in vanilla. The only AI execution path is in `RebellionsCampaignBehavior` which force-executes rebel clan heroes as cleanup (90+ days after losing all settlements). Execution is effectively a player-only strategic decision.

### Fief Redistribution on Clan Destruction

When a clan is destroyed via `DestroyClanAction`, fiefs are redistributed by `FactionHelper.ChooseHeirClanForFiefs()`:
1. If clan has a kingdom and is NOT the ruling clan → fiefs go to the ruling clan
2. If clan IS the ruling clan → random eligible clan in the kingdom
3. If no eligible kingdom clan → geographically nearest non-eliminated clan with adult lords
4. Last resort → PlayerClan

## How We Override Vanilla

We intercept two independent points in the vanilla execution flow:

### Override 1 (v1.4.8, removed): Relation Penalties by GameModel Override

**`TaomExecutionRelationModel : DefaultExecutionRelationModel`**

Registered via `campaignStarter.AddModel()` in `SubModule.OnGameStart`. Bannerlord's `GameModels` system resolves the last-registered model, so our model replaces the vanilla one.

We override `GetRelationChangeForExecutingHero()`:
1. Call `base.GetRelationChangeForExecutingHero()` to get the vanilla penalty
2. Build three `SideIds` pairs through `FactionSides` (#957), each the id and culture of ONE entity: the executor from `OfPlayer()`, the victim from `OfHero(dyingHero)`, the evaluator from `Of(otherClan.MapFaction)`. The v1.5.x patches build them the same way; nothing is snapshotted, because the victim is read from the argument the engine passes in
3. Delegate to `IExecutionRelationService.GetRelationModifier()`, which resolves each side and applies the alignment logic
4. Return its delta and notification flag

The model is thin and contains **no branching at all**, per `.claude/rules/gamemodels.md` rule 4: the snapshot-or-live choice lives in `ExecutionContext`, and every decision lives in the service.

The live fallback is load-bearing rather than defensive. `HeroExecutionSceneNotificationData` calls this same model to build the pre-confirm relation preview, when no kill is in flight and nothing has been destroyed. Routing preview and applied value through one model is what keeps them agreeing; a divergence between the two is the symptom to look for when this feature misbehaves.

### Override 2 (v1.4.8, removed): Honor Penalty by Two Harmony Patches

The honor penalty is applied deep inside `KillCharacterAction.ApplyInternal()` via `TraitLevelingHelper.OnLordExecuted()` — a parameterless static method. We can't override it via GameModel, and we can't know the victim/killer inside `OnLordExecuted()` since it takes no arguments.

**Solution: Thread-local execution context.**

**Patch A — `KillCharacterAction_ApplyInternal_Patch`:**
- **Target:** `KillCharacterAction.ApplyInternal` (private static, accessed via `AccessTools.Method`)
- **Prefix:** Before `ApplyInternal` runs, if the action is `Executed` or `ExecutionAfterMapEvent`, store the victim's and killer's kingdom StringIds into `ExecutionContext` (thread-local static)
- **Finalizer:** Always clears the context, even if `ApplyInternal` throws

**Patch B — `TraitLevelingHelper_OnLordExecuted_Patch`:**
- **Target:** `TraitLevelingHelper.OnLordExecuted` (public static, patched via `[HarmonyPatch]` attribute)
- **Prefix:** Reads the thread-local `ExecutionContext`. If the execution is cross-alignment (enemy kill), returns `false` to skip the original method (no -1000 Honor XP). If context is missing, falls through to vanilla.

Both patches share `[HarmonyPatchCategory("Patch14_Execution")]` and are applied in `SubModule.OnGameInitializationFinished`.

### Thread-Local Context Pattern (v1.4.8, removed)

`ExecutionContext` uses `System.Threading.ThreadLocal<string>` for two values: `VictimKingdomId` and `ExecutorKingdomId`. This is necessary because:
- `KillCharacterAction.ApplyInternal` is the only place that has both `victim` and `killer`
- `TraitLevelingHelper.OnLordExecuted` has neither — it just applies -1000 XP to MainHero
- The call from `ApplyInternal` → `OnLordExecuted` is synchronous and on the same thread
- The Finalizer guarantees cleanup even on exceptions

## Alignment System

### Data Model

Each Bannerlord kingdom is assigned one of three sides:

```
enum FactionSide { Free, Evil, Neutral }
```

The mapping is stored in `Main/_Module/ModuleData/execution/alignment.json`. It is a single flat
table read in three roles: as kingdom ids by `GetKingdomSide`, as culture ids by `GetCultureSide`,
and as minor clan ids when `ResolveSide(id, culture)` is handed a clan. `ResolveSide` tries the id
first and falls back to the culture, which is what places a hero whose clan has no kingdom.

The authoritative id list is the file itself; the table below is the reading of it. In-game names
come from `Main/_Module/ModuleData/spkingdoms.xslt`, which renames eight vanilla kingdoms in place,
and `taom_spkingdoms.xml`, which adds the rest. **The id almost never matches the LOTR name**, so
never infer one from the other.

### Kingdom ID Reference

| Kingdom StringId | LOTR name | Side | Vanilla origin |
|---|---|---|---|
| `empire_w` | Gondor | Free | Western Empire |
| `vlandia` | Rohan | Free | Vlandia |
| `sturgia` | Dale | Free | Sturgia |
| `erebor` | Erebor | Free | Custom kingdom |
| `rivendell` | Rivendell | Free | Custom kingdom |
| `lothlorien` | Lothlorien | Free | Custom kingdom |
| `mirkwood` | Mirkwood | Free | Custom kingdom |
| `lindon` | Lindon | Free | Custom kingdom |
| `arthedain` | Arthedain | Free | Custom kingdom |
| `eredluin` | Ered Luin | Free | Custom kingdom |
| `empire` | Dunland | Evil | Empire (main) |
| `empire_s` | Mordor | Evil | Southern Empire |
| `aserai` | Harad | Evil | Aserai |
| `khuzait` | Rhun (Easterlings) | Evil | Khuzait |
| `isengard` | Isengard | Evil | Custom kingdom |
| `gundabad` | Gundabad | Evil | Custom kingdom |
| `dolguldur` | Dol Guldur | Evil | Custom kingdom |
| `goblin` | Goblin-town | Evil | Custom kingdom |
| `mistymountainorcs` | Misty Mountain orcs | Evil | Custom kingdom |
| `bluecraig` | Blue Craig | Evil | Custom kingdom |
| `battania` | Khand | Neutral | Battania |
| `umbar` | Umbar (Corsairs) | Neutral | Custom kingdom |
| `shaghana` | Shaghana | Neutral | Custom kingdom |
| `abanissa` | Abanissa | Neutral | Custom kingdom |
| `cardolan` | Cardolan | Neutral | Custom kingdom |
| `rhudaur` | Rhudaur | Neutral | Custom kingdom |

That is every kingdom the game actually loads, verified against the live install.

### Culture ID Reference

Playable cultures nearly all share an id with their kingdom, so the same table answers both
questions. Gondor and Mordor are the two that do not, and they carry their own entries so the
culture fallback can place a kingdom-less hero:

| Culture StringId | Kingdom | Side |
|---|---|---|
| `gondor` | `empire_w` | Free |
| `mordor` | `empire_s` | Evil |

`ShippedMainCultureAlignmentCoverageTests` fails the build if a playable culture in
`taom_spcultures.xml` has no entry here, because an unlisted culture resolves Neutral and silently
switches the fallback off for anyone playing it.

### Minor Clan ID Reference

A minor clan (a mercenary company or a band of outlaws) is its own map faction while it serves no
kingdom: a hero in one has no kingdom, so the pair `ResolveSide` reads is the clan id and the clan's own
culture. The row applies only then. A mercenary contract makes the employer the clan's map faction
(`StartMercenaryServiceAction.ApplyStart` sets `Clan.Kingdom`), so a hired company's heroes, and the
player on contract, read the employer's pair and the clan row is not consulted. Most of the 14
minor clans carry a vanilla culture that is the wrong side, or that the table does not list at all.
Two of them (the Grey Company and the Corsair Blades) are retagged to the culture they belong to in
`spclans.xslt`, which also drives the engine's contract weighting (kingdoms of the clan's culture count ten
to one in `DiplomaticBartersBehavior`); the other clans that need one have a row in `alignment.json`. A row is `free` or `evil`: a `neutral`
clan row is a no-op, because `ResolveSide` falls through to the culture for an id that is listed
Neutral just as for one that is unlisted, so a clan meant to be Neutral is left unlisted and
resolves through its culture. `player_faction` is never listed.

| Clan StringId | TAOM name | Culture the clan carries | Side | Row |
|---|---|---|---|---|
| `ghilman` | Serpent Guard | `darshi` | Evil | `evil` |
| `legion_of_the_betrayed` | Grey Company | `arthedain` (retagged by `spclans.xslt`, vanilla `empire`; troops and leader clones too) | Free | none (the culture is Free) |
| `skolderbrotva` | Axemen of Erebor | `nord` | Free | `free` |
| `company_of_the_boar` | Corsair Blades | `umbar` (retagged by `spclans.xslt`, vanilla `vlandia`; troops and leader clones too) | Neutral | none (the culture is Neutral) |
| `beni_zilal` | Blind Eye | `aserai` | Evil | none (the culture is Evil) |
| `wolfskins` | Variag Ravagers | `battania` (Khand) | Neutral | none (the culture is Neutral) |
| `brotherhood_of_woods` | Dunlending Reavers | `empire` (Dunland; troops and leader clones were `vlandia`) | Evil | none (the culture is Evil) |
| `hidden_hand` | Mouth's Servants | `empire` (Dunland) | Evil | none (the culture is Evil) |
| `lakepike` | Wreckers of the Long Lake | `sturgia` (Dale) | Free | none (the culture is Free) |
| `embers_of_flame` | Cult of the Lidless Eye | `empire` (Dunland) | Evil | none (the culture is Evil) |
| `jawwal` | Sand-Riders | `aserai` | Evil | none (the culture is Evil) |
| `karakhuzaits` | Wild Easterlings | `khuzait` | Evil | none (the culture is Evil) |
| `forest_people` | Druedain | `vakken` | Free | `free` |
| `eleftheroi` | Beornings | `empire` (Dunland; troops and leader clones are `sturgia`) | Free | `free` |

The cultures are what `spclans.xslt` leaves on each clan, read from the transform output rather than the vanilla
file. The gate is `ShippedClanSideCoverageTests` (see Tests); a new minor clan fails it until it gets a deliberate side.

#### Troop and leader cultures

The clan pair above is what the heroes' sides read. The clans' troops and leader templates are separate
`NPCCharacter` rows with their own cultures, and those decide `AlignmentDesertion` (a troop whose culture side is
Free or Evil and differs from its owner's kingdom deserts; Neutral never does) and the culture-keyed rules that read
a hero's own culture. Vanilla gave four clans a culture on the opposite side of their intended one:

| Clan | Intended | Vanilla troops' and leaders' culture | Now |
|---|---|---|---|
| `legion_of_the_betrayed` | Free | `empire` (Dunland, Evil) | `arthedain` |
| `company_of_the_boar` | Neutral | `vlandia` (Rohan, Free) | `umbar` |
| `brotherhood_of_woods` | Evil | `vlandia` (Free) | `empire` |
| `eleftheroi` | Free | `empire` (Evil) | `sturgia` |

The Serpent Guard (`darshi`), Axemen of Erebor (`nord`) and Druedain (`vakken`) troops carry cultures the table does
not list, so they read Neutral and never desert; they are left alone.

**Troops** are retagged in place by `lords.xslt` (`legion_of_the_betrayed_tier_*`, `company_of_the_boar_tier_*`,
`eleftheroi_tier_*`, `brotherhood_of_woods_tier_*`). `lords.xslt` is registered for `NPCCharacters` and runs over the
accumulated document, so it reaches SandBoxCore's troops with no `SubModule.xml` row, the way it already retags
vanilla wanderer templates. Nothing but `partyTemplates.xml` and `spnpccharacters.xml` names these troops.

**Leaders** are cloned, not retagged. The `spc_*_leader_*` templates are also the vanilla `empire` (Dunland) and
`vlandia` (Rohan) cultures' `lord_templates` and `rebellion_hero_templates` (after `spcultures.xslt`: 8 of Dunland's
16 and all 8 of Rohan's), and a hero keeps its template's culture (`DefaultHeroCreationModel.GetCulture`), so
retagging them would have spawned Arthedain, Umbar, Dunland or Beorning heroes for Dunland and Rohan companion clans
and rebellions. Instead `lords.xslt` emits `taom_<original id>` beside each of the 16 originals: the original with
a new id and the clan's culture, every other attribute and child (face, equipment, skills, traits, name keys)
identical. `spclans.xslt` points each of the four clans' `minor_faction_character_templates` at the clones. The
originals and the culture lists are untouched. The clone keeps the original's `name` key; the same key with the same
default text is how vanilla already shares names between templates.

**Gate.** `ShippedClanSideCoverageTests` (see Tests).

**Saved games.** A save keeps `Clan.Culture` (a saved property) and the clan's template list
(`Clan.MinorFactionCharacterTemplates`, a saved field), and the campaign does not reload `spclans.xml` on a load
(`SandBoxManager.InitializeSandboxXMLs` reads the Kingdoms and Factions files only for a new campaign), while the
`NPCCharacters` always reload. So an old save would pair retagged troops with a clan that still carries the vanilla
culture and the vanilla `spc_*` templates: the Grey Company, clan culture `empire`, would prefer Dunland contracts
while its `arthedain` troops desert under Dunland. `MinorClanMigration`
([minor-factions.md](minor-factions.md)) corrects that on every load: the Grey Company's culture becomes `arthedain`,
the Corsair Blades' `umbar`, and each of the four clans' `spc_*` templates is replaced by its `taom_spc_*` clone
when the clone exists. It is idempotent, deterministic on every co-op peer, and changes nothing in a new
campaign. Heroes already spawned keep the culture they were created with (a hero's culture is saved with the hero);
a leader created after the update, from a clone, gets the new culture. Troop culture is not saved per party, so
troops change at once in every campaign.

Not to be confused with `Main/_Module/ModuleData/factionmap/factions.json`, which is UI-level data
for the FactionMap and uses LOTR names as `game_faction` values.

### Alignment Logic

`AlignmentService` answers four questions:

**`GetKingdomSide(string kingdomId)`** and **`GetCultureSide(string cultureId)`** return `FactionSide.Neutral` for null, empty, or unknown ids. Lookup is case-insensitive and both read the same table.

**`ResolveSide(string kingdomId, string cultureId)`** takes the kingdom side first; when that reads Neutral, the culture side; Neutral when neither classifies. The two ids must belong to the SAME entity: the fallback fires for an id that is unlisted OR listed Neutral, so a Neutral realm paired with the culture of one of its lords (a Free one) sides that lord Free, and a realm the table does not list is sided by a culture the realm does not have. Every caller therefore carries the pair whole as `SideIds(FactionId, CultureId)` and resolves it through the `ResolveSide(this IAlignmentService, SideIds)` extension (an extension rather than an interface member, so the test fakes of the two-string method keep working). `TAOM.Adapters.FactionSides` is the one place a pair is built from engine objects: `Of(faction)` is a faction's id and its own culture, `OfHero(hero)` is the hero's clan map faction with that faction's culture (a clanless hero is its own entity: no faction id, its own culture), `OfCharacter` is the same for a character or a troop, and `OfPlayer()` is the player clan's map faction. The gate is `SidePairScanTests` (see Tests). **This is what execution resolves every participant through, and it is not optional.** A kingdom id alone reads Neutral for an independent or enlisted player (enlistment deliberately does not join the commander's kingdom; a mercenary contract is not kingdomless, `Clan.Kingdom` is the employer), for a minor clan leader whose clan serves no kingdom, and for a victim whose clan was destroyed by the very kill being evaluated. A player-founded kingdom (an id such as `new_kingdom`, never listed) sides by the culture the player chose for it, because its own pair falls through to that culture. The three sibling alignment features (CaravanTrade, WarOfTheRingMomentum, PrisonerRecruitment) each carried a private copy of this fallback; execution did not, which is #556.

**`AreEnemyAlignments(string A, string B)`** — Returns `true` if the sides differ OR if either side is Neutral. Neutral kingdoms are treated as enemies of everyone, including other neutrals. This means:
- Free vs Evil = enemies
- Free vs Neutral = enemies
- Evil vs Neutral = enemies
- Neutral vs Neutral = enemies
- Free vs Free = NOT enemies
- Evil vs Evil = NOT enemies

**`AreSameAlignment(string A, string B)`** — Returns `true` only if both are the same non-Neutral side. Neutral+Neutral returns `false` — neutrals are never considered allies.

Both predicates also have `FactionSide` overloads for callers that have already resolved their sides. The string overloads delegate to them, so the Neutral semantics above have exactly one definition.

**There is deliberately no "unknown, defer to vanilla" escape hatch.** Falling back to vanilla looks conservative and is not: vanilla charges -10 to every clan leader in the world whose Honor is above 0, which in TAOM is the entire Free Peoples. One unresolved id used to hand the whole calculation to that rule. A participant that classifies on neither id now resolves Neutral and is simply nobody's ally.

## Penalty Matrix

### Cross-Alignment Execution (Good kills Evil, Evil kills Good, anyone kills Neutral)

The executor kills someone from an opposing alignment. This is a "righteous" or "expected" kill.

| Who evaluates the kill | Relation change | Honor XP | Rationale |
|----------------------|----------------|----------|-----------|
| Clan leader aligned with **executor** | **0** | — | "Good riddance" |
| Clan leader aligned with **victim** | **Vanilla** (-60/-30/-10) | — | Enemies still mourn their own |
| Neutral clan leader | **0** | — | Neutrals don't care |
| Player (Honor trait) | — | **0** | No dishonor in killing your enemy |

### Same-Alignment Execution — Kinslaying (Good kills Good, Evil kills Evil)

The executor betrays their own side. This is kinslaying.

| Who evaluates the kill | Relation change | Honor XP | Rationale |
|----------------------|----------------|----------|-----------|
| Same clan as victim | **-90** (vanilla -60 x 1.5) | — | Kinslaying penalty |
| Friend of evaluating leader | **-45** (vanilla -30 x 1.5) | — | Kinslaying penalty |
| Same faction lord | **-15** (vanilla -10 x 1.5) | — | Kinslaying penalty |
| Honorable noble | **-15** (vanilla -10 x 1.5) | — | Kinslaying penalty |
| Player (Honor trait) | — | **-1000** (vanilla) | Standard dishonor applies |

The 1.5x multiplier is defined as `KinslayingMultiplier` in `ExecutionActionHook`.

### Unclassified Factions

There is no vanilla-passthrough fallback. A participant that classifies on neither its kingdom id nor
its culture id resolves `Neutral`, and Neutral is nobody's ally and everybody's enemy, so a Neutral
executor's kill costs only the victim's own side and a Neutral evaluator never charges anything.

A stated consequence, not an accident of the predicates: **executing a Neutral-aligned lord costs
nothing from anyone, including that lord's own faction, and docks no Honor.** Khand, Umbar, Shaghana
and Abanissa are all Neutral, so a Corsair captain can be executed with no political cost at all.
That follows from "Neutral is nobody's ally", it was reviewed and kept deliberately on 2026-09-06,
and it is written down here so a future reader does not re-flag it as a gap. Making a Neutral
victim's own faction react would mean giving Neutral a same-side relation of its own, which changes
the meaning of Neutral everywhere the alignment table is used, not just in execution.

This used to be a passthrough, and that was the #556 bug: vanilla's penalty chain ends in -10 to
every clan leader in the world whose Honor is above 0, so "defer to vanilla" meant "charge the entire
Free Peoples". A missing classification must degrade to indifference, never to the vanilla rule.

## Architecture

### Component Diagram

```
alignment.json
      |
AlignmentConfigProvider ── IPathService
      |
AlignmentService (IAlignmentService)
      |
ExecutionActionHook (IOnExecutionAction) ──> ExecutionRelationService
     / \
    /   \
TraitLevelingHelper_            ExecutionCampaignBehavior_
OnBloodFeudStarted_Patch        BloodFeudRelationPenalty_Patch
(Prefix: honor hit)             (Postfix: per-clan relation penalty)
    \   /
     FactionSides builds the SideIds for both
```

### Data Flow for a Cross-Alignment Execution

```
Player clicks Execute on Mordor lord (party screen)
    |
    v
[Vanilla] HeroExecutionSceneNotificationData: relation preview
    |  (calls the static GetBloodFeudStartRelationPenaltyToOtherClan per clan,
    |   so the relation postfix shapes the preview too)
    v
Player confirms: KillCharacterAction.ApplyByExecution(victim, Hero.MainHero)
    |
    v
[Vanilla] ApplyInternal: obituary, succession, MakeDead, clan destruction
    |
    v
[Vanilla] OnHeroKilled -> ExecutionCampaignBehavior.OnPlayerExecutedHero
    |
    +-- StartBloodFeudWithClanByPlayerExecutingAHero
    |
    +-- TraitLevelingHelper.OnBloodFeudStarted(victim)
    |     [Prefix] ShouldApplyHonorPenalty(OfHero(victim), OfPlayer())
    |       Free vs Evil is cross-alignment: false, the Honor hit is skipped
    v
[Vanilla] OnBloodFeudStateChanged: for each other clan
    |  GetBloodFeudStartRelationPenaltyToOtherClan(victim, clan)
    |    [Postfix] GetRelationModifier(OfPlayer(), OfHero(victim), Of(clan.MapFaction), vanilla)
    |      executor Free vs victim Evil: cross-alignment
    |      evaluator Free?    0 (the engine skips the clan)
    |      evaluator Evil?    vanilla number
    |      evaluator Neutral? 0
    |    (the victim's kingdom is already null if his clan was just destroyed;
    |     his pair is then the clan's own, and the rest follows the same rule)
```

### Dependency Chain

```
ExecutionIoC registers:
  IAlignmentConfigProvider -> AlignmentConfigProvider (loads alignment.json)
  IAlignmentService -> AlignmentService (side lookups)
  IOnExecutionAction -> ExecutionActionHook (penalty calculations)
  IExecutionRelationService -> ExecutionRelationService

SubModule.OnSubModuleLoad:
  ExecutionIoC.InitializeHooks(executionHook, playerContext)
    -> TraitLevelingHelper_OnBloodFeudStarted_Patch.Initialize(hook)
    -> ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.Initialize(hook, playerContext)

SubModule.OnGameInitializationFinished:
  TryPatchCategory("Patch14_Execution")
    -> applies both patches
```

## Key Files

| File | Purpose | Lines |
|------|---------|-------|
| `Main/Features/Execution/FactionSide.cs` | Enum: Free, Evil, Neutral | 8 |
| `Main/Features/Execution/IAlignmentService.cs` | Alignment query interface, incl. `ResolveSide` | 47 |
| `Main/Features/Execution/SideIds.cs` | The id and culture pair of one entity | 14 |
| `Main/Features/Execution/AlignmentServiceExtensions.cs` | `ResolveSide(this IAlignmentService, SideIds)` | 11 |
| `Main/Adapters/FactionSides.cs` | The one place a `SideIds` is built from engine objects (`Of`, `OfHero`, `OfCharacter`, `OfPlayer`) | 57 |
| `Main/Features/Execution/AlignmentService.cs` | Kingdom/culture side lookups, `ResolveSide`, side predicates | 66 |
| `Main/Features/Execution/IAlignmentConfigProvider.cs` | Config loader interface | 8 |
| `Main/Features/Execution/AlignmentConfigProvider.cs` | Reads alignment.json via IPathService | 44 |
| `Main/Features/Execution/ExecutionIoC.cs` | IoC registration + hook init | 26 |
| `Main/Features/Execution/IExecutionRelationService.cs` | `ExecutionRelationResult` and the relation service contract | 51 |
| `Main/Features/Execution/ExecutionRelationService.cs` | Relation decision: side resolution, kinslaying, notification | 59 |
| `Main/Features/Execution/Hooks/IOnExecutionAction.cs` | Hook interface for both seams (honor penalty, relation modifier, the bereaved test) | 38 |
| `Main/Features/Execution/Hooks/ExecutionActionHook.cs` | Honor-penalty decision, delegation to the relation service | 44 |
| `Main/Features/Execution/Hooks/ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.cs` | Harmony postfix: rewrites the per-clan relation penalty | 82 |
| `Main/Features/Execution/Hooks/TraitLevelingHelper_OnBloodFeudStarted_Patch.cs` | Harmony prefix: skips the honor penalty | 54 |
| `Main/_Module/ModuleData/execution/alignment.json` | Kingdom, culture and minor clan id to side (36 entries: 30 kingdoms, `gondor`, `mordor`, 4 minor clans) | 34 |

### Modified Files

| File | Change |
|------|--------|
| `Main/IoC.cs` | Added `ExecutionIoC.RegisterExecutionFeature(container)` |
| `Main/SubModule.cs` | Added using statements, `ExecutionIoC.InitializeHooks` in `OnSubModuleLoad`, the `Patch14_Execution` category in `OnGameInitializationFinished` |

## Tests

### `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs` (36 tests)

Tests the `AlignmentService` with a mocked `IAlignmentConfigProvider`:
- `GetKingdomSide` and `GetCultureSide`: Free, Evil, Neutral, unknown and null (Neutral)
- `AreEnemyAlignments` and `AreSameAlignment`: every pairing of the three sides, null kingdoms
- `ResolveSide`: a listed id wins over the culture; a null, empty, unlisted or Neutral-listed id falls back to the culture of the same entity; neither classified is Neutral; the `SideIds` extension equals the string overload

### `TAOM.Tests/Features/Execution/ExecutionActionHookTests.cs` (12 tests)

Tests the hook with mocked `IAlignmentService` and `IExecutionRelationService`:
- `ShouldApplyHonorPenalty`: cross-alignment (false), same alignment (true), a kingdom-less executor, a victim whose kingdom the clan destruction nulled
- `GetRelationModifier`: returns the service's delta, passes zero through unrounded, passes every pair through intact, asks the service with notifications off
- `IsPlayerTheBereaved`: the player's clan, another clan, an unknown clan

### `TAOM.Tests/Features/Execution/ExecutionRelationServiceTests.cs` (17 tests)

Tests the decision with a mocked `IAlignmentService`: a kingdom-less executor, a victim whose clan was destroyed by the kill, an evaluator with no kingdom, a participant classified on neither id, cross-alignment zeroing, kinslaying at 1.5 times, and the notification flag.

### `TAOM.Tests/Features/Execution/ShippedClanSideCoverageTests.cs` (14 tests, `LiveInstall`)

The data gate for the alignment table (#957). It runs the real `spclans.xslt` over the installed SandBox `spclans.xml`
and adds `characters/clans.xml`, the way the engine loads them (and the same for the kingdoms, with
`spkingdoms.xslt` and `taom_spkingdoms.xml`, and for the clans' troops and leader templates with `lords.xslt`), and
reads the rows through the real `AlignmentConfigProvider`. It reports Inconclusive when the game install is absent.
It fails when:
- the set of minor clans differs from the intended-side table in the test, or a minor clan does not resolve its intended side on its own id and culture (the Grey Company and the Corsair Blades pass only on their retagged cultures)
- a troop or a named leader template reads the opposite of its clan's side, does not resolve to an `NPCCharacter`, or a leader clone differs from its original beyond id and culture
- the load migration's target cultures or clones differ from the shipped data
- a kingdom clan resolves Neutral on its kingdom's pair outside the four Neutral realms, or its own pair (its id and its own culture, which is how a victim whose clan the kill destroyed reads) differs from its kingdom's side
- `player_faction` is listed, a listed clan row is not `free` or `evil`, an `alignment.json` key names no loaded culture, kingdom or clan (a typo guard), or a row is listed for a clan id that is also a culture or kingdom id (a bandit clan shares its id with its culture)

### `TAOM.Tests/Features/Execution/SidePairScanTests.cs` (3 tests)

The source gate (#957). It reads the IL of `TAOM.dll`, so it sees what the compiler emitted: a target-typed `new(` in
any position, an object initializer, a `with` expression, a call inside a lambda or an iterator. Callers are keyed
by their outermost type. It fails when a type outside an allow-list (each entry names why its pair is one entity's)
calls `ResolveSide(id, culture)` with two arguments on the interface or the concrete service, when a type other than
`FactionSides` and the `SideIds` record constructs or copies a `SideIds`, and when an allow-list entry no longer
matches a caller. A control test compiles each shape into the test assembly and requires the scan to find it.
`AlignmentServiceTests.ResolveSide_SideIds_MatchesTheStringOverload` pins the extension.

## Vanilla Classes Referenced

All re-verified against decompiled Bannerlord v1.4.8 DLLs (2026-09-06, #556):

| Class | Namespace | Why Referenced |
|-------|-----------|---------------|
| `KillCharacterAction` | `TaleWorlds.CampaignSystem.Actions` | Harmony patch on `ApplyInternal` — the core kill method |
| `KillCharacterAction.KillCharacterActionDetail` | (nested enum) | Values `Executed` (6) and `ExecutionAfterMapEvent` (7) |
| `TraitLevelingHelper` | `TaleWorlds.CampaignSystem.CharacterDevelopment` | Harmony patch on `OnLordExecuted()` — the honor penalty method |
| `ExecutionRelationModel` | `TaleWorlds.CampaignSystem.ComponentInterfaces` | Abstract base class for our GameModel override |
| `DefaultExecutionRelationModel` | `TaleWorlds.CampaignSystem.GameComponents` | Concrete base class we inherit from |
| `CharacterRelationCampaignBehavior` | `TaleWorlds.CampaignSystem.CampaignBehaviors` | Calls our model for each clan leader on `OnHeroKilled` |
| `PartyScreenLogic` | `TaleWorlds.CampaignSystem.Party` | `IsExecutable()` — eligibility check (not patched) |
| `HeroExecutionSceneNotificationData` | `TaleWorlds.CampaignSystem.SceneInformationPopupTypes` | Execution cutscene + relation preview UI (not patched) |
| `Hero` | `TaleWorlds.CampaignSystem` | `Clan.Kingdom.StringId` — how we determine alignment |
| `ChangeClanLeaderAction` | `TaleWorlds.CampaignSystem.Actions` | Successor selection + 70% relation inheritance (not patched) |
| `DestroyClanAction` | `TaleWorlds.CampaignSystem.Actions` | Clan elimination + fief redistribution (not patched) |

## How to Add a New Kingdom

1. Add a new entry to `Main/_Module/ModuleData/execution/alignment.json` with the kingdom's StringId and side
2. No code changes needed: `AlignmentService` reads the JSON at startup. `ShippedClanSideCoverageTests` fails when a kingdom's clans resolve Neutral outside the four Neutral realms

## How to Add a Minor Clan

A minor clan the game loads must get a deliberate side: add its row to `IntendedMinorClanSides` in
`ShippedClanSideCoverageTests` and, unless its culture already resolves the intended side, a `free` or
`evil` row to `alignment.json` (never `neutral`: leave a Neutral clan unlisted). Do not list
`player_faction`. The gate is `ShippedClanSideCoverageTests`; restart the game to read the file. If the clan's troops carry a culture on the opposite side, retag them and clone the leader templates in `lords.xslt`, and add the clan to `MinorClanMigrationService` so an old save follows.

## How to Change the Kinslaying Multiplier

Edit `KinslayingMultiplier` in `Main/Features/Execution/Hooks/ExecutionActionHook.cs`. Currently `1.5f` (50% harsher than vanilla). Setting to `1.0f` would match vanilla for kinslaying; `2.0f` would double vanilla penalties.

## How to Test In-Game

1. Start a campaign as Gondor (empire_w)
2. Capture a Mordor (empire_s) lord in battle
3. Open party screen → right-click the prisoner → Execute
4. **Expected:** Relation preview shows 0 changes for Free Peoples clans, normal penalties only for Evil clans. After execution, no Honor trait penalty in the character screen.
5. Capture a Rohan (empire/vlandia) lord
6. Execute them
7. **Expected:** Relation preview shows -90 for same-clan (kinslaying). Honor penalty applies normally.
8. Capture a lord of a Neutral realm (Khand, `battania`), ideally one born into a Free culture, and execute that lord.
9. **Expected:** no Honor penalty and no relation hit from anyone, as for any Neutral lord. Before #957 a Gondor-born
   lord of Khand read as Free and the kill counted as kinslaying.

## Related Documentation

- [War of the Ring](war-of-the-ring.md) — The diplomacy system that defines hostile kingdoms
- [Architecture](../ai-includes/architecture.md) — TAOM architecture patterns
- [Harmony Patches](../../.claude/rules/harmony-patches.md) — Patch conventions
- `Main/_Module/ModuleData/diplomacy/diplomacy.json` — Kingdom relationship tiers
- `Main/_Module/ModuleData/factionmap/factions.json` — UI-level faction data (uses LOTR names, NOT kingdom StringIds)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/execution.md](./execution.md)
- [docs/features/minor-factions.md](./minor-factions.md)
- [docs/features/prisoner-recruitment.md](./prisoner-recruitment.md)
- [docs/features/tokens-of-esteem.md](./tokens-of-esteem.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/clans.md](../modding/clans.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)

<!-- backlinks-end -->
## Changelog

- 2026-10-20 (#957, part 2): every `ResolveSide` caller now passes the id and the culture of ONE entity. Added `SideIds` and the `ResolveSide(SideIds)` extension, `TAOM.Adapters.FactionSides` as the single builder, and `SideIds` on the Lord Courtesies snapshot, the Hard Questions `LordRef`, the execution seams (the patches call `FactionSides.OfPlayer()`; `IExecutionRelationService` and `IOnExecutionAction` take `SideIds`, so `ExecutionParticipant` and `IPlayerContextAdapter.GetPlayerCultureId()` are gone), tournament entrants and hosts, Wanderer Allegiance, Prisoner Recruitment, Tokens of Esteem, Good Deeds (`DeedSide` removed) and Bonds of Battle. CaravanTrade and the Momentum enrollment now ask `IAlignmentService.ResolveSide` instead of keeping private copies of its fallback. Four minor clan rows (`ghilman`, `skolderbrotva`, `forest_people`, `eleftheroi`) in `alignment.json` place the clans whose vanilla culture is unlisted or the wrong side (Axemen of Erebor, Druedain, Beornings, Serpent Guard), and `spclans.xslt` gives the Grey Company the clan culture `arthedain` and the Corsair Blades `umbar` (so the Corsair Blades are Neutral, matching TAOM's Umbar). `lords.xslt` retags the troops of the Grey Company, Corsair Blades, Beornings and Dunlending Reavers and clones their 16 leader templates as `taom_spc_*` with the new cultures (`spclans.xslt` names the clones), so a hired company's own troops never desert under an employer of its side; the block is a hand block in `lords.xslt` that `tools/complete_lords_xslt.py --apply` never rewrites (#959). A new behavior, `MinorClanMigration`, brings an old save's clan cultures and template lists to the same values on load. New gates `ShippedClanSideCoverageTests` and `SidePairScanTests`. RCA: `docs/reviews/rca-tokens-of-esteem-sides-2026-10-20.md`, part 2 (review waves 1 and 2).
- 2026-09-06 (#556): fixed the fall-through that made this whole feature skippable. `ExecutionRelationService` and the honor patch both handed the calculation back to vanilla whenever any one kingdom id was null or empty, and vanilla charges -10 to every honourable clan leader in the world. Reachable two ways: a kingdom-less executor (independent, mercenary, or enlisted player), and a victim whose `Clan.Kingdom` was nulled by the clan destruction that step 10 performs before step 11 fires the relation pass. Added `IAlignmentService.ResolveSide(kingdomId, cultureId)` plus `FactionSide` overloads of the two predicates, moved the relation service onto `ExecutionParticipant`, extended `ExecutionContext` to snapshot culture as well as kingdom for both victim and executor, added `IPlayerContextAdapter.GetPlayerCultureId()`, and deleted the dead duplicate relation logic (`ExecutionActionHook.GetRelationModifier` / `IsKinslaying`). New build-time gate `ShippedMainCultureAlignmentCoverageTests`. Corrected the kingdom mapping tables above, which had `vlandia` as Arthedain and `empire` as a free Rohan; the XSLT says `vlandia` is Rohan and `empire` is Dunland. The deep review then found that the Harmony finalizer cleared the snapshot unconditionally, so a nested kill during clan destruction wiped it mid-flight; ownership now runs through `ExecutionContext.TrySet` / `ClearIfOwned` and Harmony's `__state`. RCA: `docs/reviews/rca-execution-alignment-fallthrough-2026-09-06.md`.
- 2026-05-14 — Phase 9b extraction (#147): pulled `IExecutionRelationService` out of `TaomExecutionRelationModel` (returns `ExecutionRelationResult { RelationDelta, ShowNotification }`), reduced the model body to a single-call delegate, and replaced direct `Hero.MainHero.MapFaction.StringId` access with injected `IPlayerContextAdapter.GetPlayerKingdomId()`.
- 2026-03-25 — Introduced the Alignment-Aware Execution System: `Main/Features/Execution/` with `TaomExecutionRelationModel` GameModel override + `KillCharacterAction.ApplyInternal` / `TraitLevelingHelper.OnLordExecuted` Harmony patches (`Patch14_Execution`), `alignment.json` (16 kingdoms → Free/Evil/Neutral), zero-penalty cross-alignment kills, 1.5x kinslaying penalties, and 28 tests.
