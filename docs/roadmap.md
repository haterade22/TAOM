# TAOM Roadmap — GameModel Override Opportunities

Remaining GameModel override opportunities for making TAOM more LOTR-authentic.
TAOM currently has **31 of 121** available GameModels overridden.
Each override follows the established TAOM pattern (feature module + service + adapter + tests + JSON config).

## Implemented (no longer roadmap items)

| Model | Feature | Notes |
|-------|---------|-------|
| ~~`AgeModel`~~ | `RaceAge` | ✅ `TaomAgeModel` |
| ~~`PregnancyModel`~~ | `RaceAge` | ✅ `TaomPregnancyModel` |
| ~~`PartySizeLimitModel`~~ | `CulturalFeats` | ✅ `TaomPartySizeModel` |
| ~~`PartySpeedModel`~~ | `CulturalFeats` | ✅ `TaomPartySpeedModel` |
| ~~`CombatSimulationModel`~~ | `BattleBalance` | ✅ `TaomCombatSimulationModel` |
| ~~`DiplomacyModel`~~ | `Diplomacy` | ✅ `TaomDiplomacyModel` |
| ~~`SettlementLoyaltyModel`~~ | `CulturalFeats` | ✅ `TaomSettlementLoyaltyModel` |
| ~~`ClanFinanceModel`~~ | `CulturalFeats` | ✅ `TaomClanFinanceModel` |
| ~~`TournamentModel`~~ | `Arena` | ✅ `TaomTournamentModel` |
| ~~`SettlementProsperityModel`~~ | `CulturalFeats` | ✅ `TaomSettlementProsperityModel` |

## Remaining Opportunities

### Tier 1 — Race & Lifespan (Highest Visual Impact)

| Model | Override Goal |
|-------|---------------|
| `AgentStatCalculateModel` | Race-based stat bonuses (Uruk strength, Elf agility) |

### Tier 2 — Army & Campaign

| Model | Override Goal |
|-------|---------------|
| `BattleMoraleModel` | Racial fearlessness (Undead), cultural bravery |

### Tier 3 — Economy & Society

| Model | Override Goal |
|-------|---------------|
| `CharacterDevelopmentModel` | Race-locked skill caps |

### Tier 4 — Polish

| Model | Override Goal |
|-------|---------------|
| `MapVisibilityModel` | Ranger scouting range, Orc night vision |
| `DefectionModel` | Racial loyalty (Dwarves don't defect) |

## Recommended Implementation Order

1. `AgentStatCalculateModel` (Tier 1 — race stat bonuses)
2. `BattleMoraleModel` (Tier 2 — racial morale)
3. `CharacterDevelopmentModel` (Tier 3 — race skill caps)

## Notes

- All new overrides follow the pattern in `.claude/rules/gamemodels.md`
- Research the `Default*` base class before implementing each override
