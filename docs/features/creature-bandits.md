# Creature Bandits

## Overview

Hostile bandit parties made of creatures that fight with no rider. The first is the Spiders of Mirkwood: roaming
broods of giant spiders, led by a pale broodmother, that spawn around Mirkwood and Dol Guldur. In battle each
spider is a riderless creature that hunts the nearest enemy and bites; on the map the brood shows as a spider,
and meeting it goes straight to attack or leave. Creatures are bandits only: never recruited, never taken
prisoner, never fielded by lords.

**Status (2026-09-28):** route A (soldiers target the riderless spider) passed its Custom Battle spike: nine
console-spawned spiders were targeted, hit and killed by infantry and archers, with no crash. The creature's own
tree, its tuning and its damage-taken rules, the deployment hold and the whole campaign path (broods on the map,
a brood battle with its deployment screen, the map icon, no parley, no prisoners) have **not yet run in game**.
Research, engine evidence and the design review: [creature-bandits-roadmap.md](../research/creature-bandits-roadmap.md).

**New campaigns only.** The brood clan is a `Faction` in XML, which the engine reads only when a campaign starts
(`SandBoxManager.cs:380-384`). A save from before this version keeps playing with no broods; the spawner logs that
once.

## Why This Exists

- **Vanilla behavior:** a creature exists in battle only as a mount under a rider. A riderless mount has no team,
  no formation and no combat AI, panics on its first hit and flees off the map.
- **TAOM requirement:** a player asked for creature bandits that fight on their own. Middle-earth's Mirkwood
  spiders are the obvious first case.
- **Without this feature:** the only spider in TAOM is a goblin's mount.

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
- **Campaign.** `CreatureBroodSpawnBehavior` keeps up to four broods around Mirkwood while the MCM switch is on;
  `TaomBanditDensityModel` caps vanilla's map-wide looter spawn for the clan at zero, switch or not. `Patch94` draws
  the map icon as the spider alone and skips the encounter conversation. `TaomBattleRewardModel` refuses creatures
  as prisoners (`CreatureBanditAgents.RefusesPrisoner`).
- **Recognising a creature.** Every check is `CreatureBanditAgents.Is`: a riderless, non-humanoid agent whose
  `Character` is a creature troop. No vanilla agent matches, because the engine never sets `Character` on a
  Monster-built agent. It reads `IsHuman`, never `IsMount`, because route A clears `Mountable`.
- **Diagnostics (temporary).** `Diagnostics/` logs every creature from spawn to summary under
  `[CreatureBandits][diag]`: engine callbacks are captured on any thread and queued, and a main-thread ticker
  writes the lines under a budget (25 per creature per kind, 1,500 per mission; deaths, backstops, panics,
  flights, mounts and the side counts are exempt from the mission cap, since the creature count bounds them). The
  roadmap's "In-game order" lists which line answers which playtest question. See "Stripping the diagnostics".

### Component Diagram

```
CreatureBanditsConfig (troop ids, brood clan, anchors)   CreatureBanditTuning (MCM "Creature Bandits")
            |                                                       |
     CreatureBanditRules (pure decisions)                           |
      /        |          \                \                        |
Patch93      Patch94   CreatureBroodSpawn  TaomBattleRewardModel /  |
(battle)    (map UI)   Behavior (campaign) TaomBanditDensityModel / |
   |                                       morale models            |
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
| Creature Hit Points | 200 | 50 to 1000 | at the creature's spawn |
| Bite / Pounce / Swipe: Max Soldiers Hit | 1 / 2 / 3 | 1 to 10 | at each attack |
| Bite / Pounce / Swipe Damage % | 100 / 100 / 50 | 0 to 300 | at each attack |
| Only Crits Knock Down | on | on or off | at each attack |
| Pounce Cooldown (s) | 5.0 | 0.5 to 30 | at the creature's spawn |
| Swipe Cooldown (s) | 2.0 | 0.5 to 30 | at the creature's spawn |
| Missile Damage Taken % | 50 | 0 to 200 | at each hit |
| Cut / Pierce / Blunt Damage Taken % | 100 / 100 / 100 | 0 to 200 | at each hit |

### Compile-time: `Main/Features/CreatureBandits/CreatureBanditsConfig.cs`

| Field | Value | Meaning |
|---|---|---|
| `CreatureTroopIds` | the three spider troops | every troop that spawns as a creature |
| `BroodClanId` | `mirkwood_spiders` | the brood's bandit clan |
| `BroodAnchorSettlementIds` | Mirkwood villages and castles, Dol Guldur | where broods spawn and patrol |
| `MaxBroods` | 4 | broods alive at once; one new brood a day below it |
| `SpawnRadiusDays` | 0.25 | spawn distance from the anchor |
| `DefaultSpawnBroods` | true | the MCM switch's default |

Data: `characters/creature_bandits.xml` (the troops), the `mirkwood_spiders` culture (`taom_spcultures.xml`) and
clan (`characters/clans.xml`), and `mirkwood_spiders_brood_template` (`taom_partyTemplates.xml`), whose first stack
is the broodmother (the map icon's party leader). `CreatureBanditDataTests` pins the XML to the C# ids.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CreatureBandits/CreatureBanditRules.cs` | Pure decisions |
| `Main/Features/CreatureBandits/Hooks/CreatureBanditSpawner.cs` | Riderless spawn and wiring |
| `Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs` | Spawn swap, no panic, no rout, weapon guards |
| `Main/Features/CreatureBandits/Hooks/Patch94_CreatureBroodCampaign.cs` | Map icon, no parley |
| `Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs` | The fingerprint, and the rider, morale-panic and prisoner refusals |
| `Main/Features/CreatureBandits/CreatureWeaponStateScope.cs`, `Hooks/Patch93_CreatureBanditWeaponState.cs`, `Hooks/CreatureRouteAUnmount.cs` | Route A: weapon state at creation, then unmount |
| `Main/Features/CreatureBandits/CreatureBanditBehaviorTree.cs`, `CreatureBanditMissionBehavior.cs` | The creature's own tree, its attach, and the routed-count backstop |
| `Main/Features/CreatureBandits/BehaviorTreeElements/` | Creature gate, deployment gate, hold and hunt |
| `Main/Features/CreatureBandits/CreatureBroodSpawnBehavior.cs` | Mirkwood brood spawner |
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
- **Data**: the live `TAOM_Map` settlements (the 13 anchors and the clan's home, `village_M1_1`) and the Armory
  spider mount items.

## Tests

- `TAOM.Tests/Features/CreatureBandits/CreatureBanditRulesTests.cs`: every rule, including the deployment gate,
  the routed backstop's mount condition, the patrol rule and the MCM switch.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditDataTests.cs`: the troop XML matches the catalogue; hidden,
  bandit, a spider in every Horse slot, no weapon; the pale broodmother leads; the clan and its template match
  the C# clan id.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditsWiringTests.cs`: the module, both patch categories and
  their phases, every patch parameter bound against the installed engine, the hot-method exclusions, the model
  overrides and what they call (prisoners, morale, damage in both game types), the tree split and its deployment
  gate, and the prisoner rule itself.
- `TAOM.Tests/Features/CreatureBandits/CreatureDiagFormatTests.cs` and `CreatureDiagLedgerTests.cs`: the log line
  format, the line budget and its exemptions, and the stuck, contact-stall, whiff, engage and side-stall checks.
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
  postfix (`Mission.CanAgentRout`) run for every agent, often on the engine's worker threads. Each exits at the
  first managed field read for a non-creature and allocates nothing, and all four targets are on
  `PatchShieldPolicy.ExcludedTargetMethods`, so no per-call finalizer runs on them.
- The hunt walks the hostile teams' active agents about four times a second per creature, allocation-free.
- `CreatureBanditDamage` costs a creature victim one tuning read per hit; every other victim exits at one field
  read.
- The diagnostics are budgeted (see above) and write nothing per frame.

## Known Limitations

- **Scripted blows skip the damage-taken rules.** TAOM's own blows (`CustomAttacksUtils.TakeDamage`: troll brute
  force, signature strikes, warg and ridden-spider bites) do not pass the engine's damage model, so the Damage
  Taken options do not apply to them. No difference at the shipped 100% melee defaults; arrows are unaffected
  (Mike, 2026-09-28).
- **Deployment.** The creatures hold still until the battle starts, but vanilla hides only human defenders when the
  player attacks, so the spiders stay visible on the deployment screen.
- **A spawn that throws after the native creation call** (the render preload, the June crash site) leaves an orphan
  native agent: the fallback still spawns the troop's husk so the side can deplete, and the WARNING says the agent
  had been created. Never seen in play.
- **Broods can spawn in the player's sight**; vanilla looters retry up to 15 spawn points to avoid it.
- **Loot:** a defeated brood drops no items (the spider mounts are not merchandise).
- **Map speed:** every stack is `default_group="Cavalry"`, so a brood earns the cavalry map-speed bonus that looters
  do not.
- **Rescued looters join a brood.** A brood that beats a party holding bandit prisoners frees them into its roster, as
  vanilla does for any bandit winner (`DefaultBattleRewardModel.cs:253-275`); they fight as ordinary looters.
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
- `CreatureBroodSpawnBehavior.cs`: the `CreatureBroodCampaignDiag` calls (session, census, spawn, stray).
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

## GitHub Issue

- **Issue:** #692: [Creature Bandits: riderless giant spider broods in Mirkwood](https://github.com/haterade22/TAOM/issues/692)
- **Status:** Open
