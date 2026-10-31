namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>Small valid factions.json and maps.json texts (single quotes keep the C# readable; Newtonsoft accepts them).</summary>
internal static class FordsConfigFixtures
{
    internal const string Factions = @"{
  'factions': {
    'gondor': {
      'name': '{=TAOM_FOW_Faction_Gondor}Gondor',
      'culture': 'gondor',
      'units': [
        { 'id': 'militia', 'troop': 'gondor_ano_militia', 'role': 'Infantry', 'packSize': 10, 'price': 100 },
        { 'id': 'riders', 'troop': 'gondor_ano_mt_cavalry', 'role': 'Cavalry', 'packSize': 5, 'level': 21 }
      ],
      'heroes': [ { 'id': 'lord_1_9', 'price': 1000 } ],
      'freeWaves': [
        { 'fromMinute': 10, 'units': [ { 'troop': 'gondor_ano_footman', 'count': 8 } ] },
        { 'fromMinute': 0, 'units': [ { 'troop': 'gondor_ano_militia', 'count': 6 } ] }
      ]
    },
    'mordor': {
      'name': '{=TAOM_FOW_Faction_Mordor}Mordor',
      'culture': 'mordor',
      'units': [ { 'id': 'grunts', 'troop': 'mordor_uruk_grunt', 'role': 'Infantry', 'packSize': 10, 'price': 90 } ],
      'heroes': [],
      'freeWaves': [ { 'fromMinute': 0, 'units': [ { 'troop': 'mordor_uruk_grunt', 'count': 6 } ] } ]
    }
  }
}";

    internal const string Maps = @"{
  'defaults': {
    'options': { 'laneCount': 3, 'laneSpread': 45, 'waypointStep': 12, 'towerFractions': [0.36, 0.2], 'keepInset': 0.04 },
    'towerPrefab': { 'prefab': 'siege_barricade_a', 'scale': 1.0 },
    'keepPrefab': { 'prefab': 'siege_barricade_a', 'scale': 1.6 },
    'camp': { 'troopId': 'hill_troll', 'count': 2, 'reward': 250 }
  },
  'scenes': [
    { 'scene': 'taom_rohan_battle_001_forceatmo', 'name': '{=aom_taom_rohan_battle_001_forceatmo_name}[Rohan] Rohan Plains', 'layout': 'generated' }
  ]
}";
}
