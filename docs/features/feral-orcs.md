# Feral Orcs

## Overview

The Feral Orcs are a bandit faction of masterless orcs and goblins. They live in 23 dens spread over the
north and middle of the map, and their roaming parties come out of those dens. They answer to no Dark Lord,
so their gear is whatever they took: Mordor, goblin, Gundabad and Dol Guldur pieces mixed in one party.

## Why This Exists

- **Vanilla behavior:** a bandit clan's roaming parties spawn near the hideouts its culture holds
  (`BanditSpawnCampaignBehavior`, driven by infested-hideout counts).
- **TAOM requirement:** an orc threat that is not tied to one kingdom's territory, and that wears a mixed kit
  rather than one realm's uniform.
- **Without this feature:** the only orc bandits are the Gundabad raiders, whose 20 hideouts sit in the
  Misty Mountains, and they wear only Gundabad kit.

## Architecture

### Design Challenge

Vanilla has no "hideouts plus map-wide spawn" shape. A culture with `can_have_settlement="false"` spawns
looter-style around every town and village but cannot hold hideouts; a culture with
`can_have_settlement="true"` holds hideouts and spawns only near them. Map-wide reach therefore comes from
the number and spread of the dens, not from a spawner.

A bandit clan whose culture owns no hideout throws `KeyNotFoundException` in `GetInfestedHideoutCount` on a
new game. The clan row and the dens must ship together.

### Solution Approach

Data only. The faction copies the shape of `gundabad_raiders` (see
[bandit-management.md](bandit-management.md) "How to add a new bandit culture"):

| Piece | Where | Id |
|---|---|---|
| Troops | `Main/_Module/ModuleData/troops/troops_feral_orcs.xml` | `feral_orcs_snaga`, `_archer`, `_raider`, `_brute`, `_boss` |
| Culture | `taom_spcultures.xml` | `feral_orcs` (`is_bandit`, `can_have_settlement="true"`) |
| Clan | `characters/clans.xml` | `feral_orcs`, home `Settlement.hideout_feral_1` |
| Party templates | `taom_partyTemplates.xml` | `feral_orcs_raider_party_template`, `feral_orcs_boss_party_template` |
| Dens | live `TAOM_Map/ModuleData/settlements.xml` | `hideout_feral_1` to `hideout_feral_23` |
| Den names | live `TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml` | `Settlements.Settlement.name.hideout_feral_N` |

The faction also joins these lists: `HideoutDescriptionService` (`taom_hideout_desc_feral_orcs`),
`VoiceAccentMap` (silent, as Gundabad), `BannerBearerConfig` (`standard_of_fury_t1`),
`clan_heraldry/bandits.json`, and `CultureDataFixture.HideoutOnlyCultures`. It has no bandit voice rows and
no alignment row (bandits resolve Neutral).

## Troops

Every troop has `occupation="Bandit"`, `culture="Culture.feral_orcs"` and `is_hidden_encyclopedia="true"`.
Each carries up to four inline rosters, each mixing the four orc kits.

| Troop | Race | Level | Role | Upgrades to |
|---|---|---|---|---|
| `feral_orcs_snaga` | goblin | 6 | one-handed axe and small shield | raider, archer |
| `feral_orcs_archer` | goblin | 11 | goblin or Mordor ladder bow, axe sidearm | |
| `feral_orcs_raider` | orc | 11 | sword, axe or mace with a Gundabad shield | brute |
| `feral_orcs_brute` | uruk | 16 | spear or polearm, no shield | |
| `feral_orcs_boss` | uruk | 26 | hideout boss, heavy mixed kit | |

The brute carries no shield because its spears are two-handed polearms (`audit_polearm_shield_parity.py`).
The archer belongs to the goblin line in `tools/ranged_ladders.json`.

## Dens

Mike placed the 23 dens in the map editor. `posX`/`posY` are the scene entity's `x`/`y`. All use the mountain
hideout prefab, `map_icon="bandit_hideout_c"`, and alternate the stock SandBox scenes
`mountain_hideout_002_sv` (odd ids) and `mountain_hideout_004_sv` (even ids). The name is "Feral Orc Den" in
English, with a written translation in each of the 12 languages.

To add a den: place a `hideout_feral_N` entity in the editor, copy a den block in the live `settlements.xml`
with the new id and position, add its name row to all 12 `loc_settlements.xml` files, then rebuild the
settlement distance cache (MCM, Map Tools).

## Gates

| Gate | Checks |
|---|---|
| `BanditHideoutLiveDataTests` (`LiveInstall`) | every hideout bandit clan owns a live hideout of its culture; the `feral_orcs` clan is homed on a live feral den; each den scene exists in some module's `SceneObj`; each den has a name row in all 12 languages |
| `HideoutBossPartyTemplateTests` | boss template shape |
| `python tools/validate_moduledata.py` | item, troop and template refs, ladders |

The dens live in an unversioned module. A reinstall of an older `TAOM_Map` drops them and fails
`BanditHideoutLiveDataTests` before a new game crashes.

## How to verify in game

1. Restart Bannerlord, then start a new campaign. A loaded save has no Feral Orcs.
2. Open the encyclopedia clan list and find "Feral Orcs" among the bandit clans.
3. Travel to `hideout_feral_1` (west of the Misty Mountains, near 611, 1144) and confirm feral orc parties
   roam near it.
4. Attack a party. Check that the troops wear mixed Mordor, goblin, Gundabad and Dol Guldur gear and that no
   troop spawns naked.
5. Enter an infested den. Check that the description reads "Deep in a scarred hollow of the wilds..." and
   that the boss fight starts with its taunt.

## Known limitations

- Clans and cultures are read at new-game load: a running campaign gets no Feral Orcs.
- The faction uses the Gundabad banner key unchanged.
- The culture name, orc names, troop names and den description are English in every language until the
  paid translation run (#763).
- Spread, balance and den reachability are checked only in game.
