# Feral Orcs

## Overview

The Feral Orcs are a bandit faction of masterless orcs and goblins. They live in 23 dens across the north of the
map, and their roaming parties come out of those dens. They answer to no Dark Lord,
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
`can_have_settlement="true"` holds hideouts and spawns only near infested ones. Map-wide reach therefore
comes from the spread of the dens, not from a spawner: at most the per-faction cap of dens is infested at a
time (MCM, vanilla 9), picked from the 23 with a weight that favours dens far from other infested ones.

In vanilla, a bandit clan whose culture owns no hideout throws `KeyNotFoundException` in
`GetInfestedHideoutCount` on a new game. `Patch125_HideoutClanGuard` turns that into a clan that raids
nothing, but the clan row and the dens must still ship together.

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
Mike's choices from the review (2026-10-29):

- **Stolen kit stays off the ladders.** All five troops are in `_ARMOUR_LADDER_EXEMPT` (`tools/taom_schema.py`):
  they wear kingdom pieces but never set a piece's tier, so markets, loot, armoury levels and the next restat
  ignore them. The archer is in `_RANGED_LADDER_EXEMPT` and its file is on no line in `tools/ranged_ladders.json`,
  so it keeps both its goblin and its Mordor bow through a ladder apply.
- **With Culture Doctrine on (MCM, off by default) they never rout,** with the Gundabad raiders' doctrine
  (`culture_doctrines.json`). Isengard and Dol Guldur never rout either; Mordor, goblin and Misty Mountain
  orcs do.
- **They grow in the field.** Every troop is bandit-cultured, so vanilla's AI upgrade rule lets a living warband
  upgrade snaga to raider and raider to brute; the other TAOM bandit lines reuse kingdom troops and cannot.
- **Their prisoners cannot be recruited:** vanilla refuses any troop whose culture is a bandit culture.

## Dens

Mike placed the 23 dens in the map editor. `posX`/`posY` are the scene entity's `x`/`y`. Nearest town or castle,
measured on the live map on 2026-10-29:

| Dens | Region |
|---|---|
| 1 to 13, 23 | Eriador: Arthedain, Rhudaur, Cardolan, Ered Luin and the Lindon border |
| 14 to 16 | Anduin Vale and Angáladh |
| 17 | Dale |
| 18 to 21 | South Rhun and Dorwinion |
| 22 | Dol Guldur |

The map draws each den with its scene entity (the mountain hideout prefab, mesh `map_icon_bandit_hideout_d`); no
managed code reads the XML row's `map_icon`, which copies Gundabad's `bandit_hideout_c`. The dens alternate the stock
SandBox scenes
`mountain_hideout_002_sv` (odd ids) and `mountain_hideout_004_sv` (even ids). The name is "Feral Orc Den" in
English, with a written translation in each of the 12 languages.

To add a den: place a `hideout_feral_N` entity in the editor, copy a den block in the live `settlements.xml`
with the new id and position, add its name row to all 12 `loc_settlements.xml` files, then rebuild the
settlement distance cache (MCM, Map Tools).

## Gates

| Gate | Checks |
|---|---|
| `BanditHideoutLiveDataTests` (`LiveInstall`) | every hideout bandit clan (culture `is_bandit` and `can_have_settlement`, read from `taom_spcultures.xml`) owns a live hideout of its culture; the `feral_orcs` clan is homed on a live feral den; each den names exactly one scene, and that scene exists in some module's `SceneObj`; each den has a name row in all 12 languages |
| `Patch125_HideoutClanGuard` | runtime: a clan whose culture has no den spawns nothing instead of crashing a new game; a den whose culture has no clan stays empty instead of crashing an incident ([registry](../reference/harmony-patch-registry.md#patch125_hideoutclanguard)) |
| `HideoutBossPartyTemplateTests` | boss template shape |
| `python tools/validate_moduledata.py` | item, troop and template refs, ladders |

The dens live in an unversioned module. A reinstall of an older `TAOM_Map` drops them and fails
`BanditHideoutLiveDataTests`; in game the clan then raids nothing and Patch125 logs one warning.

## How to verify in game

1. Restart Bannerlord, then start a new campaign. A save made before this change shows the 23 dens but never
   fills them (see Known limitations).
2. Rebuild the settlement distance cache (MCM, TAOM, Map Tools); the popup confirms its check.
3. Hover a roaming party near a den: its tooltip names "Feral Orcs" and lists `[Feral]` troops. (The encyclopedia
   clan list shows no bandit clans, and the first dens a new game fills are a weighted random pick, so
   `hideout_feral_1` may be empty.) Confirm feral orc parties
   roam near it.
4. Attack a party. Check that the troops wear mixed Mordor, goblin, Gundabad and Dol Guldur gear and that no
   troop spawns naked.
5. Enter an infested den. Check that the description reads "Deep in a scarred hollow of the wilds..." and
   that the boss fight starts with its taunt.

## Known limitations

- A save made before this change gets no Feral Orcs clan, because the engine reads clans only on a new game. It
  does load the culture and the 23 dens, which show on the map and stay empty ("There seems to be no one
  inside"); a den the player enters and leaves turns invisible, as any empty den does.
  `Patch125_HideoutClanGuard` keeps the Gold Rush incident from crashing on such a den.
- TAOM and TAOM_Map must ship together: the clan is in TAOM, the dens in TAOM_Map
  ([release-process.md](../reference/release-process.md)). With an older map the clan raids nothing, and
  Patch125 logs one warning.
- The faction uses the Gundabad banner key unchanged.
- The culture name, orc names, troop names and den description are English in every language until the
  paid translation run (#763).
- Spread, balance and den reachability are checked only in game.
