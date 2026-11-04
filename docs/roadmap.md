# TAOM Roadmap — GameModel Override Opportunities

Remaining GameModel override opportunities for making TAOM more LOTR-authentic.
TAOM currently has **70** GameModel overrides, the parked and abstract ones included (the list is [gamemodel-registry.md](reference/gamemodel-registry.md)).
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
| `AgentStatCalculateModel` | Race-based stat bonuses (Uruk strength, Elf agility). SLOT TAKEN: `TaomAgentStatCalculateModel` (CareerSystem) already carries the career, mount-lock, culture aggression, race ability and creature siege rules, so a static race stat rule goes into that model through its service seam, never a second `AddModel`. Race identity in melee shipped on the damage side: per-race knockdown, stagger and swing modifiers, see [combat-mechanics.md](features/combat-mechanics.md) |

### Tier 2 — Army & Campaign

| Model | Override Goal |
|-------|---------------|
| `BattleMoraleModel` | ~~Cultural bravery~~ SHIPPED: `TaomBattleMoraleModel` (CultureDoctrine, #608) sets which cultures never rout (`CanPanicDueToMorale`) and how brave each starts (`GetEffectiveInitialMorale`), see [culture-doctrine.md](features/culture-doctrine.md). A race-keyed fearlessness rule joins that model, never a second `AddModel`. **Racial dread is done without an override**: the DreadAura feature CALLS `CalculateMoraleChangeToCharacter` for tier/hero resistance and drives `CommonAIComponent` morale from a `MissionLogic`, because the caller applies the sign and an override would shrink morale GAINS too. Racial fearlessness (`CanPanicDueToMorale`) is the one goal here still open, and the rule belongs in that model: see [dread-aura.md](features/dread-aura.md) "Rejected seams" |

### Tier 3 — Economy & Society

| Model | Override Goal |
|-------|---------------|
| `CharacterDevelopmentModel` | Race-locked skill caps. SLOT TAKEN: `TaomCharacterDevelopmentModel` (Seasoned Hosts, #890) raises the learning rate's minimum past a skill's limit; a race skill cap rule goes into that model, not a second one |

### Tier 4 — Polish

| Model | Override Goal |
|-------|---------------|
| `MapVisibilityModel` | ~~Ranger scouting range, Orc night vision~~ SLOT TAKEN: `TaomMapVisibilityModel` (CareerSystem) registered in `SubModule.OnGameStart` alongside the other campaign models (grep `TaomMapVisibilityModel` in `SubModule.cs`); FieldCamp's lookout, Far Watch's scout and Under the Eaves' forest penalty also layer through it. New spotting logic joins that model via its service seam, never a second `AddModel` |
| `DefectionModel` | ~~Racial loyalty (Dwarves don't defect)~~ SLOT TAKEN: `TaomDefectionModel` (Sworn Anew, #869) lets a captured lord be asked to join the player's kingdom, gated by alignment and the lore heroes; a free hero still falls through to vanilla, so racial loyalty for free lords is still open |

## Recommended Implementation Order

1. `AgentStatCalculateModel` (Tier 1, race stat bonuses; the rule joins `TaomAgentStatCalculateModel`)
2. `BattleMoraleModel` (Tier 2, now only race-keyed fearlessness, which joins `TaomBattleMoraleModel`; cultural bravery shipped in CultureDoctrine and racial dread shipped without an override)
3. `CharacterDevelopmentModel` (Tier 3, race skill caps; the slot is taken by Seasoned Hosts, so the rule joins `TaomCharacterDevelopmentModel`)

## Notes

- All new overrides follow the pattern in `.claude/rules/gamemodels.md`
- Research the `Default*` base class before implementing each override

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dread-aura.md](features/dread-aura.md)
- [docs/reference/doc-lookup.md](reference/doc-lookup.md)

<!-- backlinks-end -->
