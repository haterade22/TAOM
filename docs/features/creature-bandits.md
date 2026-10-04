# Creature Bandits

## Overview

Hostile bandit parties made of creatures that fight with no rider. The first is the Spiders of Mirkwood: roaming
broods of giant spiders, led by a pale broodmother, that spawn around Mirkwood and Dol Guldur. In battle each
spider is a riderless creature that hunts the nearest enemy and bites; on the map the brood shows as a spider,
and meeting it goes straight to attack or leave. Creatures are bandits only: never recruited, never taken
prisoner, never fielded by lords.

The second is the Wild Trolls (#694): bands of two to four hill trolls (for now), about one per kingdom, roaming
near that kingdom's towns, castles and villages. A troll is humanoid, so it fights as a troll troop with every troll
trait, wearing no armour but with 300 hit points and taking 70% of every hit; like a spider it is never taken
prisoner or recruited, a band never takes in freed prisoners, and meeting a band goes straight to attack or leave.

**Status (2026-09-28):** route A (soldiers target the riderless spider) passed its Custom Battle spike: nine
console-spawned spiders were targeted, hit and killed by infantry and archers, with no crash. The creature's own
tree, its tuning and its damage-taken rules, the deployment hold and the whole campaign path (broods on the map,
a brood battle with its deployment screen, the map icon, no parley, no prisoners) have **not yet run in game**.
Research, engine evidence and the design review: [creature-bandits-roadmap.md](../research/creature-bandits-roadmap.md).
Troll bands in game (2026-09-29): Mike spawned one with the console command (then led by an armoured cave troll),
met it on the map (no parley), fought it, and saw a lord's army destroy it with its trolls refused as prisoners.
After that test the armour came off, the bands became hill trolls only and gained their hit points and damage rule;
none of that, nor the no-join and freed-prisoner rules or the twenty-brood cap, has run in game yet. The map icon is
drawn from the race's `as_hill_troll_map` action set. See "In-game checklist".

**New campaigns only.** The brood and troll clans are `Faction`s in XML, which the engine reads only when a campaign
starts (`SandBoxManager.cs:380-384`). A save from before them keeps playing without them; each spawner logs that
once.

## Why This Exists

- **Vanilla behavior:** a creature exists in battle only as a mount under a rider. A riderless mount has no team,
  no formation and no combat AI, panics on its first hit and flees off the map.
- **TAOM requirement:** a player asked for creature bandits that fight on their own. Middle-earth's Mirkwood
  spiders are the obvious first case.
- **Without this feature:** the only spider in TAOM is a goblin's mount.
- **Troll bands (#694):** Mike wanted trolls as a real threat across the map, one band per kingdom and hard to
  beat (two to four trolls), where before a troll appeared only in Mordor's lord parties.

## Architecture

### Design Challenge

The engine has no native AI or roster support for a riderless combatant (roadmap, "Can a creature fight with no
rider?"). The June 2026 riderless spider crashed on formation membership and on its uninitialised native weapon
state, and was paused. This design spawns the spider through the engine's own free-mount path, adds only what a
troop needs, in order, without a formation, and then makes it an enemy the engine's target selection accepts
(route A).

### Solution Approach

- **Troop to creature.** Each creature is a hidden troop whose Horse slot holds its creature item. In a field
  battle, on the enemy side, `Patch93_CreatureBanditSpawn` replaces `Mission.SpawnTroop` for that troop:
  `CreatureBanditSpawner` calls `Mission.SpawnMonster`, then sets the troop's origin (casualties and battle end),
  the troop as `Character` (no NRE on the first kill, and kill XP), the tuned hit points, the team, clears the
  flight flags, applies route A and raises `OnAgentBuild`. Any other spawn, a banner bearer's spawn, or a failed
  one stays vanilla: the troop's goblin husk on its spider.
- **Targetable (route A).** The engine only lets soldiers target an enemy, and it takes a riderless mount's side from
  its missing rider. `Patch93_CreatureBanditWeaponState` creates the creature with a native weapon state (which the
  soldiers' target scorer reads unchecked), and `CreatureRouteAUnmount` clears `Mountable` after the team is set.
  The full chain is in the roadmap's "Targeting" section.
- **Stays in the fight.** `Patch93_CreatureBanditNoPanic` (the riderless mount's hit panic) and
  `Patch93_CreatureBanditNoRout` (the edge fade-out) keep it fighting, and the morale route to panic is refused in
  the morale models' `CanPanicDueToMorale` (`TaomBattleMoraleModel`, `TaomCustomBattleMoraleModel`).
  `CreatureBanditMissionBehavior` counts a routed creature SandBox would skip (only one route A left Mountable).
- **Hunts and bites, in its own tree.** `CreatureBanditBehaviorTree`, attached by `CreatureBanditMissionBehavior`
  (the ridden spider's `SpiderMissionBehavior` skips creature bandits, so no spider carries two trees): the spider's
  engage gate, pounce and swipe, plus `CreatureHuntTask`, which moves the creature toward the nearest enemy on a
  hostile team and holds it under a scripted order during a bite.
- **Waits out deployment.** The deployment screen pauses and hides formation soldiers only, and while it is up a
  scripted move teleports (`Agent.cs:2462-2465`). `CreatureMayFightDecorator` gates the fight on the engine's own
  signals (`AllowAiTicking`, `IsTeleportingAgents`), and until then `CreatureHoldTask` keeps the creature where it
  stands.
- **Its own numbers (MCM "Creature Bandits").** `CreatureBanditTuning` holds the C# defaults the MCM options
  override, all clamped (see Configuration). A capped strike hits the nearest soldiers in its arc, a rider and his
  horse counted once, and never a riderless loose mount, which would otherwise take a soldier's slot (a riderless
  elephant still carrying howdah crew counts as one; the crew are struck as soldiers). The ridden spider keeps
  `SpiderConfig` (`SpiderStrikeSet.Ridden`).
- **On the scoreboard.** The engine's battle observer counts humans only (`BattleObserverMissionLogic.cs:35-76`), so
  `CreatureScoreboardBridge` reports each creature itself: one troop when built, its casualty when removed (once,
  and only if it was added), and the kill credit between a creature and a soldier either way. A console spawn in
  Custom Battle has no party, so it shows under a generic "Party" row.
- **Damage taken.** `CreatureBanditDamage` scales every connected blow on a creature in the damage model's
  `ApplyDamageReductions`: in the campaign's `TaomCombatMechanicsModel`, and in Custom Battle through
  `TaomCustomBattleCreatureDamageModel`, which extends the engine's own Custom Battle model and is declared by the
  feature module. A charge, kick, bash or hilt hit counts as Blunt, as vanilla computes it.
- **Campaign.** `CreatureBroodSpawnBehavior` keeps up to twenty broods around the 47 Mirkwood and Dol Guldur
  settlements (every town, castle and village of both cultures on the live map), one new brood a day, while the
  MCM switch is on; `TaomBanditDensityModel` caps vanilla's map-wide
  looter spawn for both creature clans at zero, switch or not. `Patch94` draws the brood's map icon as the spider
  alone and skips the encounter conversation for a brood or a troll band. `TaomBattleRewardModel` refuses spiders
  and bandit trolls as prisoners (`CreatureBanditAgents.RefusesPrisoner`) and, in `GetLootPrisonerChances`, keeps
  freed prisoners out of both (the other winners share the band's chance; with none left the prisoner goes free).
  `Patch94_CreatureBandNoJoin` keeps both out of the one path that bypasses the prisoner rule: with Partners in
  Crime, "serve under my command" recruits every bandit party joining the encounter, a nearby band included.
  Both spawners create and re-patrol their parties through `CreatureBandParties`, vanilla's own looter steps,
  including its up to 15 tries for a spawn point outside the player's sight.
- **Troll bands (#694).** `TrollBandSpawnBehavior` keeps about one band per living kingdom (not eliminated, owning a
  town, castle or village): each day, while there are fewer bands than living kingdoms, one band spawns near a random
  settlement of a kingdom with none and patrols there. A band counts for its home settlement's current kingdom. The
  troops are hidden twins of Mordor's `cave_troll` and `hill_troll` (same race, level, skills, face and weapons;
  own ids, bandit occupation, the `wild_trolls` culture), so Mordor's recruitable trolls are untouched and only the
  twins are refused as prisoners. The race brings every other troll trait. The twins wear no armour and are made tougher
  instead: 100 more hit points, so 300 (`CreatureBanditRules.TrollBanditHitPointsBonus` in
  `TaomCharacterStatsModel`), and 70% of every hit (`TrollBanditDamageTakenFactor` in `CreatureBanditDamage.Reduce`). A band spawns with its template's two to
  four trolls: vanilla's bandit roll stays inside each stack's range (`DefaultPartySizeLimitModel.cs:346-399`) and
  Patch39 caps its growth at each `max_value`. It stays two to four trolls and nothing else: no troll is ever a
  prisoner to free back, and the reward model gives it no freed prisoner. Which kingdoms are owed a band is the
  pure `CreatureBanditRules.KingdomsOwedATrollBand`, keyed on kingdom ids.
- **Recognising a creature.** Every check is `CreatureBanditAgents.Is`: a riderless, non-humanoid agent whose
  `Character` is a creature troop. No vanilla agent matches, because the engine never sets `Character` on a
  Monster-built agent. It reads `IsHuman`, never `IsMount`, because route A clears `Mountable`.
- **Diagnostics (temporary).** `Diagnostics/` logs every creature from spawn to summary under
  `[CreatureBandits][diag]`: engine callbacks are captured on any thread and queued, and a main-thread ticker
  writes the lines under a budget (25 per creature per kind, 1,500 per mission; deaths, backstops, panics,
  flights, mounts and the side counts are exempt from the mission cap, since the creature count bounds them). A
  caller takes its line from the budget before it formats it, so a refused line costs no string (the event lines'
  `thread=` label included, built inside the granted branch); refusals are still
  counted (`suppressed` in the snap line, `suppressedLines` per creature in the summary). A mission (any
  mission, not only a battle) in which no creature spawn was attempted or declined skips every agent callback and
  ends with one INFO line,
  `[CreatureBandits][diag] no-creatures t=312.40 note=no creature spawn was attempted or declined this mission; every agent callback exited on one field read`
  (`t` is the mission time at teardown). Its condition is the complement of the summary's, so a mission ends with
  the summary or this line, never both: a declined creature troop is reported by the summary's `declined=`. The
  roadmap's "In-game order" lists which line answers which playtest question. See "Stripping the diagnostics".

### Component Diagram

```
CreatureBanditsConfig (troop ids, clans, anchors)        CreatureBanditTuning (MCM "Creature Bandits")
            |                                                       |
     CreatureBanditRules (pure decisions)                           |
      /        |          \                \                        |
Patch93      Patch94   CreatureBroodSpawn  TaomBattleRewardModel /  |
(battle)    (map UI)   TrollBandSpawn      TaomBanditDensityModel / |
   |                   -> CreatureBandParties  morale models        |
CreatureBanditSpawner -> CreatureBanditBehaviorTree (hold, hunt, spider strikes) <-+
            \-> CreatureBanditDamage (TaomCombatMechanicsModel, TaomCustomBattleCreatureDamageModel)
```

## Configuration

### MCM: "Creature Bandits"

A top-level group, not under "Combat Mechanics": that group's master switch promises to make everything below it
inert, and none of these reads fold it. To change a default later, rename the property (orientation.md trap
"Persisted MCM defaults").

| Option | Default | Range | Read |
|---|---|---|---|
| Spawn Spider Broods | on | on or off | each day; off stops new broods, live ones stay |
| Spawn Troll Bands | on | on or off | each day; off stops new bands, live ones stay |
| Creature Hit Points | 200 | 50 to 1000 | at the creature's spawn |
| Bite / Pounce / Swipe: Max Soldiers Hit | 1 / 2 / 3 | 1 to 10 | at each attack |
| Bite / Pounce / Swipe Damage % | 100 / 100 / 50 | 0 to 300 | at each attack |
| Only Crits Knock Down | on | on or off | at each attack |
| Pounce Cooldown (s) | 5.0 | 0.5 to 30 | at the creature's spawn |
| Swipe Cooldown (s) | 2.0 | 0.5 to 30 | at the creature's spawn |
| Missile Damage Taken % | 50 | 0 to 200 | at each hit |
| Cut / Pierce / Blunt Damage Taken % | 100 / 100 / 100 | 0 to 200 | at each hit |

Every option in the table after the two switches tunes the spiders only; a bandit troll's numbers are its race's plus 100 hit points and 70% of every hit.

### Compile-time: `Main/Features/CreatureBandits/CreatureBanditsConfig.cs`

| Field | Value | Meaning |
|---|---|---|
| `CreatureTroopIds` | the three spider troops | every troop that spawns as a creature |
| `BroodClanId` | `mirkwood_spiders` | the brood's bandit clan |
| `BroodAnchorSettlementIds` | the 47 towns, castles and villages of Mirkwood and Dol Guldur (`CreatureBanditLiveDataTests` keeps it equal to the live map) | where broods spawn and patrol |
| `MaxBroods` | 20 | broods alive at once; one new brood a day below it |
| `TrollBanditTroopIds` | the two troll twins | the troll band troops (the prisoner rule, their hit points and damage) |
| `TrollClanId` | `wild_trolls` | the troll bands' bandit clan |
| `TrollBanditExtraHitPoints`, `TrollBanditDamageTaken` | 100, 0.7 | a bandit troll's toughness in place of armour (Mike, 2026-09-29) |
| `SpawnRadiusDays` | 0.25 | spawn distance from the anchor, broods and bands |
| `DefaultSpawnBroods`, `DefaultSpawnTrollBands` | true | the MCM switches' defaults |

Data: `characters/creature_bandits.xml` (the spiders), the `mirkwood_spiders` culture (`taom_spcultures.xml`) and
clan (`characters/clans.xml`), and `mirkwood_spiders_brood_template` (`taom_partyTemplates.xml`), whose first stack
is the broodmother (the map icon's party leader). `CreatureBanditDataTests` pins the XML to the C# ids. The trolls:
`characters/troll_bandits.xml`, the `wild_trolls` culture and clan (home `village_R1_1`, nominal), and
`wild_trolls_band_template` (hill trolls 2 to 4, for now: a bare cave troll has no cloth); `TrollBanditDataTests`
pins them, keeps each twin equal to its Mordor troll less the armour, and keeps the twins out of every other
template and culture.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CreatureBandits/CreatureBanditRules.cs` | Pure decisions |
| `Main/Features/CreatureBandits/Hooks/CreatureBanditSpawner.cs` | Riderless spawn and wiring |
| `Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs` | Spawn swap, no panic, no rout, weapon guards |
| `Main/Features/CreatureBandits/Hooks/Patch94_CreatureBroodCampaign.cs` | Map icon, no parley, no joining the player |
| `Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs` | The fingerprint, and the rider, morale-panic and prisoner refusals |
| `Main/Features/CreatureBandits/CreatureWeaponStateScope.cs`, `Hooks/Patch93_CreatureBanditWeaponState.cs`, `Hooks/CreatureRouteAUnmount.cs` | Route A: weapon state at creation, then unmount |
| `Main/Features/CreatureBandits/CreatureBanditBehaviorTree.cs`, `CreatureBanditMissionBehavior.cs` | The creature's own tree, its attach, and the routed-count backstop |
| `Main/Features/CreatureBandits/BehaviorTreeElements/` | Creature gate, deployment gate, hold and hunt |
| `Main/Features/CreatureBandits/CreatureBroodSpawnBehavior.cs` | Mirkwood brood spawner |
| `Main/Features/CreatureBandits/TrollBandSpawnBehavior.cs` | Troll band spawner, one band per kingdom |
| `Main/Features/CreatureBandits/CreatureBandParties.cs` | The spawn and re-patrol steps both spawners and the console command share |
| `Main/Features/CreatureBandits/Cheats/CreatureBandCheats.cs` | `taom.spawn_creature_band`: one band or brood beside the player, for testing |
| `Main/Features/CreatureBandits/CreatureBanditTuning.cs`, `Main/Features/TaomSettings.cs` (group "Creature Bandits") | The creature's numbers and their MCM options |
| `Main/Features/Spider/SpiderStrikes.cs` | Strike rules as data: per-attack target cap, damage multiplier, crit-only knockdown |
| `Main/Features/CreatureBandits/Hooks/CreatureBanditDamage.cs`, `Models/TaomCustomBattleCreatureDamageModel.cs` | Damage taken, campaign and Custom Battle |
| `Main/Features/CreatureBandits/CreatureBanditLog.cs` | The error log for the spawner and the weapon-state hook |
| `Main/Features/CreatureBandits/Hooks/CreatureScoreboardBridge.cs` | Creature troops, casualties and kills on the battle scoreboard |
| `Main/Features/CreatureBandits/CreatureBanditsModule.cs` | Feature module wiring |
| `Main/Features/CreatureBandits/Diagnostics/` | Temporary playtest logging (mission and campaign) |

## Dependencies

- **Spider** (`Main/Features/Spider/`): the spider Monster, its clips, its engage gate and its pounce and swipe
  tasks, run with the creature's strike rules. The ridden spider's own tree skips creature bandits.
- **CombatMechanics** and **CareerSystem**: the campaign damage model carries the damage-taken rule, and the blunt
  rule is `TaomAgentApplyDamageModel.BluntByVanillaRule`.
- **CultureDoctrine**: both morale models and the Custom Battle stat model (ride lock) carry a creature clause.
- **CulturalFeats** (`TaomBattleRewardModel`, prisoners) and **BanditManagement** (`TaomBanditDensityModel`, the
  looter cap).
- **Behaviour tree framework** (`Main/BehaviorTrees`, `CreatureTreeTracker`), and `AdvancedCombat` for the
  synthetic blow and the agent slot check.
- **Data**: the live `TAOM_Map` settlements (the 47 anchors and the clans' homes, `village_M1_1` and
  `village_R1_1`), the Armory spider mount items, and for the trolls the Armory's troll races, their `_map` action
  sets and the Mordor trolls' weapons.

## Tests

- `TAOM.Tests/Features/CreatureBandits/CreatureBanditRulesTests.cs`: every rule, including the deployment gate,
  the routed backstop's mount condition, the patrol rule, the MCM switch, the cap of 20, and the troll rules (the
  twins, both clans, the prisoner rule, the kingdoms owed a band, the freed-prisoner renormalisation).
- `TAOM.Tests/Features/CreatureBandits/CreatureBandCheatsTests.cs`: `taom.spawn_creature_band`'s parser (a typo is an
  error, `confirm` is literal) and pins for its quarter-sight point and every refusal.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditLiveDataTests.cs` (LiveInstall): the brood anchors equal every
  Mirkwood and Dol Guldur town, castle and village on the live map, and both troll `_map` action sets exist.
- `TAOM.Tests/Features/CreatureBandits/TrollBanditDataTests.cs`: the troll XML matches the catalogue and loads in the
  campaign; each twin is a hidden `wild_trolls` bandit equal to its Mordor troll less the armour; the clan and
  culture are a bandit looter shape; the band template holds two to four hill trolls; no other template or culture
  names a twin.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditDataTests.cs`: the troop XML matches the catalogue; hidden,
  bandit, a spider in every Horse slot, no weapon; the pale broodmother leads; the clan and its template match
  the C# clan id.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditsWiringTests.cs`: the module, both patch categories and
  their phases, every patch parameter bound against the installed engine, the hot-method exclusions, the model
  overrides and what they call (prisoners, morale, damage in both game types), the tree split and its deployment
  gate, the prisoner rule itself, the troll spawner and both-clan seams (no parley, looter cap, no join, freed
  prisoners), the out-of-sight spawn and the switch defaults.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditWieldGuardTests.cs`: the three wield guards on bare agents of
  nine kinds (soldier, husk rider, mounts, creatures with and without route A), each answering as the predicate did
  before plan 032; its untagged `CreatureBanditAgentsTests` pin the null agent and, in the IL, that a humanoid is
  ruled out by its flags before the troop id is read.
- `TAOM.Tests/Features/CreatureBandits/CreatureDiagFormatTests.cs` and `CreatureDiagLedgerTests.cs`: the log line
  format, the line budget and its exemptions, `AnyRegistered` (the callbacks' no-creature gate), and the stuck,
  contact-stall, whiff, engage and side-stall checks.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditDiagTests.cs` (`RequiresGame`): `TakeLine` takes the budget
  before a line is formatted (no logger, within budget, the one cap WARNING; the per-creature cap refuses without a
  WARNING and counts `suppressedLines`), `Emit` routes by level, the
  `no-creatures` line is written, literally, only when no creature spawn was registered, attempted or declined, and
  `WriteEvent` allocates nothing for any event kind the budget refuses (read from
  `GC.GetAllocatedBytesForCurrentThread`) while a granted line still ends with `thread=<id>/main` or `/off`.
- `TAOM.Tests/Features/DevConsole/MissionSpawnOriginTests.cs`: console spawns get the right origin in Custom Battle
  and in the campaign.
- `TAOM.Tests/Features/CreatureBandits/CreatureWeaponStateScopeTests.cs`: the one-shot creation scope. The rules
  tests pin the route A flag transforms, and the wiring tests bind the `CreateAgent` hook to the installed engine.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditTuningTests.cs`: the defaults, the clamps, the damage-taken
  factors with vanilla's blunt rule, and the top-level MCM group. `TAOM.Tests/Features/Spider/SpiderStrikeTests.cs`
  and `SpiderStrikeCapTests.cs`: the strike rules, the nearest-N pick, a rider and his horse in one slot, and the
  cap through the service; `SpiderAttackServiceTests` prove the ridden spider keeps its numbers.

## How to Add a Creature (the mountain spider)

1. Add a hidden troop to `characters/creature_bandits.xml` with the creature item in its Horse slot, and its id
   to `CreatureBanditsConfig.CreatureTroopIds`. The data test fails until both match.
2. Add a stack to the brood template, after the broodmother.
3. If it uses the spider Monster, `CreatureBanditBehaviorTree` already hunts and bites for it. A creature on another
   Monster needs its own tree: `CreatureBanditMissionBehavior` attaches the spider tree to every creature bandit,
   and the spider's clips would never play on another action set (`AgentAdapter` refuses a clip the set lacks), so
   it would hunt but never bite. Gate the attach by Monster first.
4. Add its id to `_HARNESSLESS_BY_DESIGN` in `tools/taom_schema.py` (a harness in its Horse slot would be worn by
   the wild creature), register and translate its name (`/localize`).

## Performance

- Three weapon guards (`GetPrimaryWieldedItemIndex`, `GetOffhandWieldedItemIndex`, `GetMissileRange`) and the rout
  postfix (`Mission.CanAgentRout`) run for every agent, often on the engine's worker threads. Each allocates
  nothing; a mount exits at its managed `Character` field (null for every ordinary mount), a soldier after that and
  one native flags read (`IsHuman`), and only a non-humanoid agent with a `Character` reads its troop id and rider
  (plan 032). All four targets are on
  `PatchShieldPolicy.ExcludedTargetMethods`, so no per-call finalizer runs on them.
- The hunt walks the hostile teams' active agents about four times a second per creature, allocation-free.
- `CreatureBanditDamage` costs a creature victim one tuning read per hit; every other victim exits at one field
  read.
- The diagnostics are budgeted (see above), take a line from the budget before formatting it
  (`CreatureBanditDiag.TakeLine`, then `Emit`), and write nothing per frame; in a mission with no creature every
  agent callback exits on one field read (`CreatureDiagLedger.AnyRegistered`).

## In-game checklist (#694)

Restart the game, then start a **new campaign**. The `[CreatureBandits][diag]` lines are in `taom_debug_*.log`.
To meet one at once, with cheat mode on: `taom.spawn_creature_band trolls confirm` (or `broods`) spawns one beside
your party; without `confirm` it is a dry run. It shares the spawners' party step but none of their daily rules: it
ignores the MCM switches and the cap, still takes a cap slot, and is homed on your nearest town, castle or village.
Use it for steps 3 to 7; run steps 1, 2, 8 and 9 on spawner bands, first or on another save. While enlisted, the band
spawns beside your commander's column.

1. `campaign-start` shows `missingAnchors=-`, and the `daily` line's `broods=` climbs by one a day to 20.
2. Over the first weeks the `troll-daily` line's `kingdomsOwed=` falls to 0, `looterCap=0`, and its `list` shows
   each band with its kingdom and 2 to 4 troops.
3. A band's map icon is a hill troll in its own hide, in and out of a map
   battle, with no crash on approach or zoom.
4. Meeting a band skips the conversation (a `no-parley` line with a `wild_trolls` party id).
5. In battle the trolls fight like Mordor's hill troll (brute force, no armour) but tougher: 300 hit points and 70%
   of every hit. Does a band of two to four feel hard to beat? After a win no troll is taken prisoner
   (`prisoner-refused troop=taom_troll_bandit_*`).
6. With Partners in Crime, talk down a looter party next to a troll band and pick "serve under my command": the
   looters join, the trolls do not and stay on the map.
7. Lose to a troll band while holding looter prisoners: the band's `list` entry keeps its troll count only.
8. New `brood-spawn` lines with `origin=spawner` show `fromPlayer` above `playerSight` (a retry is accepted on path
   distance, so a line just under it can still be right; `origin=console` lines are placed beside you on purpose).
9. MCM "Spawn Troll Bands" off: no new bands; live ones stay.
10. An older save: one "No 'wild_trolls' clan" line; its broods grow to 20 over the new anchors.
11. Custom Battle: `taom.spawn_troops taom_troll_bandit_hill 2 enemy` spawns trolls that fight normally.

## Known Limitations

- **Scripted blows skip the damage-taken rules.** TAOM's own blows (`CustomAttacksUtils.TakeDamage`: troll brute
  force, signature strikes, warg and ridden-spider bites) do not pass the engine's damage model, so the Damage
  Taken options do not apply to them. No difference at the shipped 100% melee defaults; arrows are unaffected
  (Mike, 2026-09-28). A bandit troll takes those blows whole, not at 70%; armour never reduced them either.
- **Deployment.** The creatures hold still until the battle starts, but vanilla hides only human defenders when the
  player attacks, so the spiders stay visible on the deployment screen.
- **A spawn that throws after the native creation call** (the render preload, the June crash site) leaves an orphan
  native agent: the fallback still spawns the troop's husk so the side can deplete, and the WARNING says the agent
  had been created. Never seen in play.
- **Broods and bands can still spawn in the player's sight** when 15 tries find no point outside it, as vanilla's
  looters can.
- **Loot:** a defeated brood drops no items (the spider mounts are not merchandise).
- **Map speed:** every stack is `default_group="Cavalry"`, so a brood earns the cavalry map-speed bonus that looters
  do not.
- **A band may leave a kingdom bare.** A band counts for its home settlement's current kingdom, so one whose home
  was captured, or is held by a clan outside any kingdom, still counts toward the cap: its old kingdom gets a band
  only once one dies. A player's own kingdom counts as living and gets a band too.
- **A console troll in Custom Battle has 200 hit points.** Custom Battle reads health from the race Monster, not
  `TaomCharacterStatsModel`, so only the 70% damage rule reaches it there; the campaign's bands get the full 300.
- **Broods thin vanilla's looters.** Vanilla spawns fewer looters around a settlement with several looter-faction
  parties homed there (`BanditSpawnCampaignBehavior.GetSpawnChanceInSettlement`), so Mirkwood villages with two
  or more broods see fewer vanilla looters.
- **The creation scope is not reentrancy-safe.** If a creation listener ever spawned an agent on the same thread
  inside `Mission.CreateAgent`, the nested postfix would take the outer creature's strip. No engine or TAOM listener
  does today (Codex review 2026-09-28, O1).

## Stripping the Diagnostics

After sign-off, delete `Main/Features/CreatureBandits/Diagnostics/` and every call into it. Outside the folder:

- `CreatureBanditsModule.cs`: the diagnostics mission behavior and campaign behavior lines.
- `Main/SubModule.cs`: `CreatureBanditDiag.ResetForUnload()` (keep `CreatureBanditLog.ResetForUnload()`).
- `Hooks/CreatureBanditSpawner.cs`: the ledger record, the `CreatureDiagSpawn` breadcrumbs and counters, and the
  `CreatureDiagRecord` parameter of `Wire`. Keep the `CreatureBanditLog` error and fallback lines.
- `Hooks/Patch93_CreatureBandits.cs`: the `Note*` calls in each patch. `Hooks/Patch93_CreatureBanditWeaponState.cs`:
  `CreationHookSites`. `Hooks/Patch94_CreatureBroodCampaign.cs`: the map icon and no-parley notes.
- `Hooks/CreatureBanditAgents.cs`: the three `Note*` calls; the refusals themselves stay.
- `CreatureBanditMissionBehavior.cs`: the backstop's counter and event (the backstop stays).
- `CreatureBroodSpawnBehavior.cs`: the `CreatureBroodCampaignDiag` calls (session, census; keep the missing-anchor
  warning). `CreatureBandParties.cs`: the spawn and stray calls. `TrollBandSpawnBehavior.cs`: the troll census call.
- `BehaviorTreeElements/CreatureHuntTask.cs`: `NoteHunt` and `NoteStaleHandle`.
- `Main/Features/Spider/BehaviorTreeElements/SpiderEngageDecorator.cs` (the engage census) and
  `SpiderAttackTaskBase.cs` (`NoteAttack`).

## Changelog

- 2026-09-27: built: riderless spawn, stay-in-fight patches, hunt and bite, Mirkwood brood spawner, map icon,
  no parley, no prisoners (#692). Playtest diagnostics added; the console spawn command now finds troops in
  Custom Battle. After the first spike showed soldiers never target a riderless spider: route A, which makes the
  spider an enemy to the engine's own target selection.
- 2026-09-28: route A passed its Custom Battle spike (soldiers and archers target, hit and kill the spiders; no
  crash). Then the creature's own tree, and its own tunable numbers and damage-taken rules (MCM), after the spike
  showed an uncapped bite striking 23 soldiers and archers killing a 120 HP spider in 3 to 5 s. Deep review: the
  MCM group moved out of Combat Mechanics, the creature holds during deployment, the brood switch (default on),
  the blunt rule, the morale rule moved from a patch to the morale models, the diagnostics made strippable. Codex
  review: creatures now show on the battle scoreboard, and a loose horse no longer takes a capped strike's slot.
- 2026-09-28: up to twenty broods over all 23 Mirkwood and Dol Guldur settlements, and the Wild Trolls: bands of two
  to four bandit trolls, about one per kingdom, never prisoners, no parley, with their own MCM switch (#694). Deep
  review: the 24 castle-bound villages added (47 anchors, pinned to the live map), no brood or band joins the player
  through the bandit join path, bands stay trolls only, spawns avoid the player's sight, one census line a day.
- 2026-09-29: `taom.spawn_creature_band trolls|broods [confirm]` (Tier C) spawns one band or brood beside the player
  for testing; `CreatureBandParties.Spawn` takes an optional point, and the `brood-spawn` line carries
  `origin=console|spawner` (#694).
- 2026-09-29: the Wild Trolls wear no armour (Mike, after the first in-game test), and the bands are hill trolls
  only for now: a bare cave troll's body has no cloth, its trousers belong to the armour mesh. The cave twin stays
  defined, armourless and in no band. In place of armour a bandit troll has 300 hit points (the race's 200 plus
  100) and takes 70% of every hit (Mike's choice).

## GitHub Issue

- **Issue:** #692: [Creature Bandits: riderless giant spider broods in Mirkwood](https://github.com/haterade22/TAOM/issues/692)
- **Status:** Closed, `triage-needs-ingame`
- **Issue:** #694: [twenty spider broods and wild troll bands, one per kingdom](https://github.com/haterade22/TAOM/issues/694)
- **Status:** Closed, `triage-needs-ingame` (the owed checks are in its closing comment)
- **Translations:** #695: [the seven Creature Bandits names into the 12 languages](https://github.com/haterade22/TAOM/issues/695), open
