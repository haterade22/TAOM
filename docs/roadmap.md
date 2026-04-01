# TAOM Roadmap — GameModel Override Opportunities

Identified 15 GameModel override opportunities for making TAOM more LOTR-authentic.
Bannerlord's GameModel system allows deep customization without Harmony patches.
Each override follows the established TAOM pattern (feature module + service + adapter + tests + JSON config).

Full implementation plan: `.claude/plans/luminous-watching-oasis.md`

## Priority Tiers

### Tier 1 — Race & Lifespan (Highest Visual Impact)

| Model | Override Goal |
|-------|---------------|
| `AgeModel` | Elven immortality, Dwarf/Hobbit lifespans, Orc aging |
| `PregnancyModel` | Race-appropriate pregnancy durations |
| `AgentStatCalculateModel` | Race-based stat bonuses (Uruk strength, Elf agility) |

**Start here.** Elven immortality is the highest-impact, most requested feature.

### Tier 2 — Army & Campaign

| Model | Override Goal |
|-------|---------------|
| `PartySizeLimitModel` | Culture/race-based warband sizes |
| `PartySpeedModel` | Terrain movement bonuses (Elves in forests, etc.) |
| `BattleMoraleModel` | Racial fearlessness (Undead), cultural bravery |
| `CombatSimulationModel` | Configurable damage ratios per battle type |

### Tier 3 — Economy & Society

| Model | Override Goal |
|-------|---------------|
| `DiplomacyModel` | Racial enmity (Elves/Orcs never ally) |
| `SettlementLoyaltyModel` | Cultural loyalty modifiers |
| `ClanFinanceModel` | Racial trade bonuses |
| `CharacterDevelopmentModel` | Race-locked skill caps |

### Tier 4 — Polish

| Model | Override Goal |
|-------|---------------|
| `MapVisibilityModel` | Ranger scouting range, Orc night vision |
| `TournamentModel` | Per-culture tournament types |
| `DefectionModel` | Racial loyalty (Dwarves don't defect) |
| `SettlementProsperityModel` | Racial settlement growth rates |

## Recommended Implementation Order

1. `AgeModel` + `PregnancyModel` (Tier 1 — race lifespans)
2. `PartySizeLimitModel` (Tier 2 — army sizes)
3. `BattleMoraleModel` (Tier 2 — racial morale)
4. `AgentStatCalculateModel` (Tier 1 — race stats)

## Notes

- TAOM currently has 22 of 121 available GameModels overridden
- All new overrides follow the pattern in `.claude/rules/gamemodels.md`
- Research the `Default*` base class before implementing each override
