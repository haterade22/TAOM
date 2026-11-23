# Codex pre-review: #1544 promoted companion gear reset

Repository: this working tree (TAOM, a Bannerlord v1.5.7 mod, .NET Framework 4.7.2). Review the
UNCOMMITTED diff (`git diff` plus the untracked `TAOM.Tests/Features/FieldCommission/CivilianKitTypeTests.cs`).
Read-only: do not edit files. Report findings with file:line, severity (P1/P2/P3), a concrete failure
scenario, and the source line that proves it. Mark anything you cannot prove from code as UNVERIFIED.

## Reported bug

A player (single player) promoted a troop to a companion through Field Commission, gave him gear, and some
time later he wore the troop's default kit again and the given items were gone.

## The maintainer's hypothesis (verify or refute it; do not assume it)

1. For a troop with no civilian set, `Hero.SetInitialValuesFromCharacter` clones a set from
   `neutral_culture`'s civilian roster, whose `<EquipmentSet>` has no civilian flag, so
   `MBEquipmentRoster.InitEquipment` types it Battle.
2. `HeroCommissionAdapter.CreateCompanionFromTroop` fills that set with `FillFrom(battleKit, false)`, which
   keeps the Battle type.
3. Vanilla `InventoryLogic.PartyEquipment.ResetEquipment` (inventory Cancel/Reset) routes each saved copy by
   its type, so the Battle-typed civilian copy is written into `FirstBattleEquipment` after the real battle
   copy, and the player's gear is lost.

Engine source: the installed DLLs under the game's `bin/Win64_Shipping_Client`
(`pwsh tools/taom-src.ps1 path <Type>` decompiles one), or the dump at
`E:/Decompiled_Bannerlord/_categories_v1.5.7/`. SandBoxCore data: the game's
`Modules/SandBoxCore/ModuleData/sandboxcore_equipment_sets.xml`.

## The change

- `HeroCommissionAdapter.MakeCivilian(Equipment)`: retypes a set Civilian in place via
  `new Equipment(Civilian).FillFrom(src,false)` then `src.FillFrom(typed,true)`. Called at promotion.
- `HeroCommissionAdapter.RepairCivilianKitType(heroId)`: on load, retypes a surviving promoted hero's civilian
  set; skips the campaign-wide `DeadCivilianEquipment` fallback.
- `FieldCommissionBehavior.OnGameLoaded`: runs the repair over the list `PruneDeadPromotedHeroes` returns
  (authority only).
- `FieldCommissionOfferFlowService.PromoteNow(troopId, name)`: the creation half of an accepted offer,
  factored out of `OnRenamed`; `OnRenamed` now calls it and then `CompleteOffer`.
- Dev console `taom.fc_add_companion <troopId> [name]` calls `PromoteNow`.

## Known suspects to check

- Is the hypothesis right? Is there any other path that would produce the reported symptom?
- `FillFrom(typed, true)`: does it copy anything besides the type and 12 slots (for example a
  `SyncEquipments` flag, item modifiers, the slot-fit check `IsItemFitsToSlot`) that changes behaviour?
- Does a Civilian-typed set with non-civilian items break anything (inventory civilian tab, settlement spawn,
  `Equipment.IsItemFitsToSlot`, `EquipmentHelper`)?
- Load path: does `OnGameLoaded` run before or after anything that reads the civilian set? Is
  `PruneDeadPromotedHeroes`'s returned list safe to iterate while the repair runs?
- Co-op: the repair runs on the authority only. Does a type change desync clients?
- `PromoteNow` refactor: any behaviour change for the offer path (order of RecordPromotedHero vs
  CompleteOffer, null name)?
- Siblings: `Main/Features/Refuge/WardenService.cs:391` also builds a hero from a troop with
  `CreateSpecialHero`. Same defect reachable?
