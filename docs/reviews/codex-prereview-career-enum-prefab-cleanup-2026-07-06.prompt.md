# Codex Pre-Review: career-enum-prefab-cleanup (2026-07-06)

Review the UNCOMMITTED changes in this repo (`git diff` + `git status`). Scope: a mechanical
cleanup/retune pass over the CareerSystem:

1. `PassiveEffectType` enum: 15 unused members deleted, 10 members renamed to project vocabulary
   (map below), members regrouped. The enum is parsed from XML strings
   (`taom_career_choices.xml` `type=` attributes) and never persisted to saves.
   Rename map: ShruggedOff→ShrugOff, EnchantmentCostReduction→SmithingCostReduction,
   HorseChargeDamage→MountChargeDamage, HorseHealth→MountHealth, TroopRegeneration→TroopSurvival,
   HealthRegeneration→HeroHealing, BattleRenownGain→RenownGain,
   CustomResourceGain→SpecialResourceGain, CustomResourceUpkeepModifier→SpecialResourceUpkeepModifier,
   CustomResourceUpgradeCostModifier→SpecialResourceUpgradeCostModifier.
2. `CareerConfigProvider.ParseChoice`: new warning when a PassiveEffect `type=` attribute does not
   parse (previously silently coerced to `Special`). New test covers it.
3. `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml`: VisualDefinitions renamed
   (BottomMenu→CareerFooterSlide, TopPanel→CareerHeaderSlide, ExtendablePanel→CareerNodePanel),
   transition timings retuned, inert `EaseIn` attribute dropped, pane widths 500/1420→520/1400.
4. `CareerChoiceGroupObjectVM`: two index for-loops rewritten as LINQ FirstOrDefault/LastOrDefault.
5. Comment/hint-text cleanup across `Main/` (no behavior intended).

KNOWN SUSPECTS — verify each adversarially:

A. **Rename completeness.** Grep the ENTIRE repo (`Main/`, `TAOM.Tests/`, `tools/`,
   `Main/_Module/ModuleData/`) for any of the 10 OLD names surviving. A missed XML attr now
   parses as `Special` (inert pip) — the new warning would fire, but it is still a regression.
   Note: `isShruggedOff` in `TaomCombatMechanicsModel.cs` is a TaleWorlds engine parameter name
   and must NOT have been renamed — verify it was not.
B. **Deleted members.** Verify none of the 15 deleted names (WindsOfMagic, WindsCostReduction,
   WindsRegeneration, PrayerCoolDownReduction, WindsCooldownReduction, SpellRadius,
   SpellEffectiveness, AccuracyPenalty, RangedMovementPenalty, EquipmentWeightReduction,
   MoraleDamageToEnemyOnKill, DebuffDuration, BonusDamageShield, TroopSkill, UnitPartyWeight)
   is still referenced in code, XML, or tests (historical docs excluded).
C. **Enum ordinal safety.** Confirm no code casts `PassiveEffectType` to/from int, serializes it
   by value, or depends on member order (Enum.GetValues with index math, arrays indexed by enum).
D. **Prefab integrity.** Every `VisualDefinition=` reference in CareerScreen.xml resolves to a
   defined `VisualDefinition Name`; no C# or other prefab references the old definition names;
   pane widths are sane for a 1920-wide layout.
E. **LINQ rewrite equivalence.** `FirstOrDefault(!IsTaken)` / `LastOrDefault(IsTaken)` +
   early-return-when-null must match the old index loops exactly, including invoking
   `_choiceChangedAction` only when an action occurred.
F. **Warning/parse parity.** The new unknown-type gate must match `ParseEnum`'s semantics exactly
   (case-insensitive TryParse) — a mismatch would warn on valid values or miss invalid ones.
G. **Consumers consistency.** Shipped `taom_career_choices.xml` must reference only types present
   in `PassiveEffectConsumers` post-rename (CareerChoicesIntegrationTests is the gate — verify it
   actually reads the shipped XML, not a fixture).

Output findings as: SEVERITY (P1/P2/P3) | file:line | claim | proof.
If a suspect holds, say so explicitly per suspect. Do not propose new features.
