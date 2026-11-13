# Hero Auras

## Overview

Named heroes strengthen the allied soldiers near them, and each hero does it his own way. Dáin's dwarves keep
their feet and their shields up, Aragorn's men hold their nerve, the elven lords' archers draw faster and aim
truer, and the orcs near the Witch-king do not break. Only the heroes listed by hero StringId in
`hero_auras.json` carry an aura. There is no fallback by culture or race: an unlisted lord has none. A soldier's
race can limit who an aura reaches, so Dáin's aura reaches dwarves only.

An aura reaches the allied soldiers within its radius (15 m at most): the hero's own team and any team fighting
on his side, the player included, but never the hero himself. It refreshes once a second and stays on a soldier
for 3 seconds after he leaves the radius or the hero falls. Auras run in field battles, sieges and sally-outs, in a
campaign and in Custom Battle, from the moment the battle starts (not during deployment); never in hideouts, raids,
arenas or multiplayer. Status: Slice 1, not yet played in game.

## Why This Exists

- **Vanilla behaviour:** a hero changes the soldiers around him only through the captain perks of the formation
  he leads, and those come from his skills, not from who he is.
- **TAOM requirement:** player feedback (2026-11-12) asked that heroes carry an aura that makes the troops around
  them stronger or more resistant, different for each hero. The [Dread Aura](dread-aura.md) was the only presence
  effect, and it only weakens enemies.
- **Without this feature:** standing beside Aragorn or Dáin changes nothing for a soldier.

## Architecture

### Design Challenge

- **No new stat system.** The [Race Abilities](race-abilities.md) already carry the channels an aura needs
  (damage, damage reduction, knockdown, knock-back and dismount resistance, block, parry, draw speed, a morale
  floor), and six TAOM models already apply them, in the campaign and in Custom Battle.
- **No compounding.** The stat post-pass multiplies, so two sources that add would compound. A soldier under an
  aura can also have his own race ability live.
- **Any thread.** The stat and damage models run off the main thread.

### Solution Approach

- Every model reads a soldier's effects in one seam, `RaceAbilityHooks`. Its helper `EffectsOf` now merges the
  soldier's own live ability with the aura he carries, through `RaceAbilityEffectsMerge.Combine`. No new GameModel
  override and no Harmony patch.
- **The merge rule,** per channel: the stronger value counts and values never add. A negative value in the
  soldier's own ability is a price paid on purpose (berserk's block of -60 percent), so it stays. For
  `aimErrorPercent` lower is better, so the rule is mirrored. Flags are granted by either source, and the morale
  floor is the higher one. Two auras on one soldier merge by the same rule.
- The race abilities' telemetry counts the events in which a live race ability took part. A soldier with an aura
  alone adds nothing to it; on a soldier with both, the aura's share is counted under his ability.
- `HeroAuraLedger` holds each soldier's aura effects and their end time in a concurrent map of immutable
  entries, keyed by the agent reference (never `Agent.Index`). The models read it from any thread.
- The source side follows the Dread Aura: heroes are found by hero StringId through
  `DreadSourceTracker.HeroAxisId` (the hero's id in a campaign, the character's own id outside one), and a fallen
  hero leaves the source list.
- **Who is reached:** `Mission.GetNearbyAllyAgents` returns every active humanoid of a team not hostile to the
  hero's, within the radius in 2D, and it includes the hero himself; the pulse skips him. A soldier of an allied
  team (the player fighting beside a lord outside an army) is reached too.
- **Co-op:** each peer stamps only the soldiers it owns (`CoopMissionOwnership.IsOwnedHere`), near any hero it can
  see; the role is `AgentOwner` ([coop-interop.md](coop-interop.md), "Mission behaviors").
- **A throw in the pulse** stands the feature down for the mission and ends every aura already given.

### Component Diagram

```
FeatureModules -> HeroAurasModule (TaomFeatureModule)
   ├── HeroAurasMissionLogic : MissionLogic     gate, pulse clock, engine events, end line and [Check]
   │      └── HeroAuraPulse                     sources, nearby allies, stamp, stat refresh, morale top-up
   ├── HeroAuraService                          hero id -> aura, race filter, strength, merge
   │      └── HeroAurasConfigProvider           hero_auras.json, validated
   ├── HeroAuraSettingsProvider                 MCM Battle Tactics/Hero Auras
   └── HeroAuraRuntime.Ledger  ──(AuraEffects)──> RaceAbilityHooks.EffectsOf
                                                    └── RaceAbilityEffectsMerge.Combine
                                                          └── the six stat, damage and morale models
```

## Configuration

### Config File: `Main/_Module/ModuleData/hero_auras/hero_auras.json`

| Field | Meaning |
|---|---|
| `enabled` | The JSON switch, beside the MCM one |
| `pulseSeconds` | How often each aura looks for allies (0.25 to 5) |
| `lingerSeconds` | How long a soldier keeps the aura after he leaves its reach (0.25 to 10; the pulse expires in the tick it stamps, so 0 would end every aura at once) |
| `radius` | The reach of an aura that names none (1 to 15 m; past 15 m the engine's nearby-agent query walks every agent) |
| `auras[].id` | The aura's name in the logs; unique, without `+` or `@` (a later repeat is skipped with a warning) |
| `auras[].heroIds` | Hero StringIds; a hero carries at most one aura (a later one drops him with a warning) |
| `auras[].radius` | Optional own reach |
| `auras[].races` | Optional race names the aura reaches (`human`, `dwarf`, `elf`, `orc`, `goblin`, `uruk`, `uruk_hai`, `berserker`, ...); empty: every allied soldier |
| `auras[].effects` | The race abilities' effect keys ([race-abilities.md](race-abilities.md), the effect table) |

An aura only strengthens: the loader sets a negative bonus, a positive `aimErrorPercent` and the fields only a
race ability reads (`moraleOnEnd`, `healPerKill`, the fear fields) to 0 with a warning. The morale floor is capped
at 50.

MCM, `Battle Tactics/Hero Auras`: **Enable Hero Auras** (on) and **Hero Aura Strength** (0 to 2, default 1; it
multiplies the bonuses through `RaceAbilityService.Scale`, and 0 turns every aura off, because `Scale` reads a
factor of 0 or less as 1). Both settings are simulation-relevant for co-op ([coop-interop.md](coop-interop.md)).

### Current Values

| Aura | Heroes | Reaches | Effects |
|---|---|---|---|
| `aragorn_heir_of_isildur` | Aragorn (`named_companion_aragorn`) | men | morale floor 30, damage reduction 10 %, block 10 % |
| `dain_ironfoot` | Dáin II Ironfoot (`lord_E1_1`) | dwarves | knockdown resistance 40 %, knock-back resistance 30 %, block 15 % |
| `theoden_king_of_the_mark` | Théoden (`lord_4_1`) | men | morale floor 25, melee damage 8 %, mount speed 8 %, dismount resistance 30 % |
| `eomer_marshal_of_the_mark` | Éomer (`lord_4_3_1`) | men | melee damage 10 %, dismount resistance 40 %, mount speed 5 % |
| `elven_lords` | Elrond (`lord_R1_1`), Thranduil (`lord_M1_1`), Galadriel (`lord_L1_1`) | elves | draw speed 12 %, ranged damage 8 %, aim error -15 % |
| `saruman_white_hand` | Saruman (`lord_I1_0`) | uruks, Uruk-hai, berserkers | morale floor 35, melee damage 8 % |
| `witch_king` | the Witch-king (`lord_1_15`) | orcs, goblins, uruks | morale floor 40, attack eagerness 15 % |

The numbers are first drafts for tuning. Théoden is the vanilla ruler `lord_4_1`, renamed in `lords.xslt`. Aragorn
is a campaign hero only: he is not a Custom Battle commander, so his aura appears in a campaign battle only.

**Known limitations:**

- A soldier under two auras keeps them as one set. When he leaves one hero's reach, or that hero falls, the next
  pulse stamps the new set at once, so the lost aura does not linger. A soldier on the edge of a second hero's reach
  is restamped, and his stats refreshed, on each pulse that adds or drops that hero.
- Any morale floor above 0 means the soldier cannot panic at all while he carries it (`RaceAbilityService.HoldsNerve`),
  and the strength slider does not scale a floor: near Aragorn, Théoden, Saruman or the Witch-king, the reached
  soldiers never rout.
- The auras reach every soldier of the listed races: Théoden's and Éomer's reach foot soldiers too, and no channel
  adds horse-charge damage. A rider filter and a charge channel are Slice 2 candidates (#1127).
- `heroSets` and a staggered pulse, which the issue's design names, are not built: auras list `heroIds`, and every
  hero pulses on the same clock (one nearby-agent query per hero per second).

## Key Files

| File | Role |
|---|---|
| `Main/Features/HeroAuras/HeroAurasModule.cs` | Registration, the mission behavior, wires `RaceAbilityHooks.AuraEffects` |
| `Main/Features/HeroAuras/HeroAuraService.cs` | Hero id to aura, race filter, strength scaling, merge of several auras |
| `Main/Features/HeroAuras/HeroAurasConfigProvider.cs` | Loads and validates `hero_auras.json` |
| `Main/Features/HeroAuras/HeroAuraLedger.cs` | Each soldier's aura and its end; the reach count for the end line |
| `Main/Features/HeroAuras/Hooks/HeroAurasMissionLogic.cs` | Gate, pulse clock, engine events, end line and `[Check]` |
| `Main/Features/HeroAuras/Hooks/HeroAuraPulse.cs` | Sources, nearby allies, stamping, stat refresh, morale top-up |
| `Main/Features/RaceAbilities/Domain/RaceAbilityEffectsMerge.cs` | The merge rule |
| `Main/Features/RaceAbilities/Hooks/RaceAbilityHooks.cs` | `EffectsOf`: the seam every model reads |

## Dependencies

- [Race Abilities](race-abilities.md): the effect channels, `RaceAbilityService.Scale` and `MoraleTopUp`, and the
  hooks the models call.
- [Dread Aura](dread-aura.md): `DreadSourceTracker.HeroAxisId` for the hero id.
- [Signature Strikes](signature-strikes.md): `SignatureMissionGate` (combat missions, Custom Battle included, no
  multiplayer).

## Tests

`TAOM.Tests/Features/HeroAuras/`: the ledger, the aura key (`HeroAuraKeyTests`), the service (hero id lookup, race
filter, strength, merge), the MCM settings (strength 0 is off), the config provider's validation, and the shipped
config, whose every hero id must be an NPCCharacter or Hero row in the repo data and every race name one TAOM ships.
The merge rule is `TAOM.Tests/Features/RaceAbilities/RaceAbilityEffectsMergeTests.cs`. `CoopMissionRolePolicyTests`
fails while the behavior has no row in the co-op role table; it does not pin which role the row gives.

Game-tested only (ADR-008; the code needs a live `Agent`): the pulse (who is reached, the linger after a hero
falls), the mission gate, and the merge inside `RaceAbilityHooks.EffectsOf`. The in-game check and its two claims
below cover them.

## How to verify in game

1. Start a battle with a listed hero on your side, for example Dáin II Ironfoot with dwarves, in a campaign or
   in Custom Battle.
2. Read `taom_debug_*.log`: `[HeroAuras] heroes with an aura on the field: ...` after the first second, and at the
   end `[HeroAuras] Mission end: dain_ironfoot: N soldier(s)` and
   `[Check] id=taom.hero-auras.reached result=pass auras=... soldiers=...`.
3. `python tools/ingame_verdict.py --owed` reads the check from `tools/ingame_checks/1127.toml`.
4. By eye: dwarves beside Dáin are knocked down less often than dwarves far from him.

## How to add an aura

1. Find the hero's StringId in the data (an `NPCCharacter` or `Hero` row).
2. Add an entry to `auras` in `hero_auras.json` with its `heroIds`, optional `races` and `effects`.
3. Run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~HeroAura"`:
   `ShippedHeroAurasConfigTests` fails on an unknown id, an unknown race name or a rejected value.

## Performance

One `Mission.GetNearbyAllyAgents` query per hero per pulse (once a second), inside the engine's 15 m proximity
grid. A soldier whose auras did not change keeps his effects object, and his stats are not refreshed. The models
pay one concurrent-map read per call, and one merge allocation only while both an aura and the soldier's own race
ability are live.

## Changelog

- 2026-11-12: Slice 1: config, ledger, merge seam, pulse, MCM settings, end line and `[Check]` (#1127).

## GitHub Issue

[#1127](https://github.com/haterade22/TAOM/issues/1127). Slice 2 (a visible sign on soldiers under an aura, more
heroes, tooltip text) is open there.
