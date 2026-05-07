## Follow-up to #115 — FormationPresets UI capture/apply (P2-1 from Codex review #36)

The CompanionTactics port (#115, commit `5595037`) ships the FormationPresets sub-feature with UI Save / Load / Auto-Assign buttons that currently behave as Phase-1 stubs. The buttons appear, the inquiry chains work, but the underlying VM mutation is not wired up:

- `Save` persists a name-only `HoNFormationPreset` — the `HeroFormationAssignments` / `CaptainHeroIds` / `FormationClasses` dictionaries stay empty.
- `Load` shows a notification ("Preset selected for load — Load is a Phase-1 stub").
- `Auto-Assign` shows a notification ("Auto-Assign is a Phase-1 stub — feature pending").

Codex review #36 surfaced this as **P2-1** (file: `docs/reviews/codex-adversarial-companiontactics-2026-05-06.md`, lines 72–122). The user-facing promise (saved/loaded preset of OOB hero-to-formation assignments) does not match the code (name-only POCO).

## What "full implementation" needs

A boundary adapter for `OrderOfBattleVM` capture / apply. The original mod's source uses reflection on:

- `OrderOfBattleVM._allHeroes` — `private List<OrderOfBattleHeroItemVM>`
- `OrderOfBattleVM.UnassignedHeroes` — `MBBindingList<OrderOfBattleHeroItemVM>` (public property)
- `OrderOfBattleVM.Formations` — `MBBindingList<OrderOfBattleFormationVM>` (public)
- Each `OrderOfBattleFormationVM.Heroes` — `MBBindingList<OrderOfBattleHeroItemVM>` (public)
- Each `OrderOfBattleHeroItemVM.Hero` — `Hero` ref (public)

### Capture (Save)

For each hero in `vm._allHeroes`:
1. Find which `OrderOfBattleFormationVM.Heroes` collection contains it (or whether it's in `UnassignedHeroes`).
2. Record `preset.HeroFormationAssignments[hero.StringId] = formationIndex` (or `-1` for unassigned).
3. If the hero is the formation's captain (separate flag on the VM), add to `preset.CaptainHeroIds`.
4. For each formation, record its `RepresentativeClass` or `DeploymentFormationClass` into `preset.FormationClasses[formationIndex] = classValue`.

### Apply (Load)

For each `(heroId, formationIndex)` in `preset.HeroFormationAssignments`:
1. Find the `OrderOfBattleHeroItemVM` whose `Hero.StringId == heroId` in `_allHeroes`.
2. Move it from its current formation's `Heroes` collection to the target `Formations[formationIndex].Heroes`.
3. Update captain status to match `preset.CaptainHeroIds`.
4. Restore `formation.DeploymentFormationClass` from `preset.FormationClasses`.
5. Skip heroes whose `StringId` is not in `_allHeroes` (missing-hero pruning).
6. Refresh the OOB UI (`vm.Refresh()` or equivalent).

### Auto-Assign

`HeroAutoAssigner.AutoAssignHeroes(IOrderOfBattleAdapter, resetExisting)`:
1. Iterate `vm._allHeroes`, scoring each via `IHeroAutoAssigner.ScoreHeroForFormation(hero, formationClass)`.
2. Pick the formation with the highest score for each hero.
3. Move heroes into the chosen formations (using the same VM mutation path as Apply).
4. Optionally show a confirmation inquiry if `resetExisting && AnyHeroAssigned()`.

## Implementation suggestion

Add `IOrderOfBattleVMTracker.CapturePreset(string name) : HoNFormationPreset` and `ApplyPreset(string presetId) : void` to the existing `IOrderOfBattleVMTracker` (which is a boundary class — already permitted to see sealed types and use reflection).

The reflection setup should:
- Cache `FieldInfo` for `_allHeroes` once at first use
- Cache `PropertyInfo` for `Heroes` and `Hero` once at first use
- If reflection fails (e.g., field renamed in a future Bannerlord update), log a warning and surface a graceful "feature unavailable" message in the UI

Add unit tests:
- `CaptureFromVM_PopulatesAllAssignments` — proves a populated VM produces a populated `HoNFormationPreset`
- `ApplyToVM_RestoresAssignments` — proves loading mutates the VM correctly
- `ApplyToVM_MissingHero_SkipsAssignment` — proves missing-hero pruning works
- `AutoAssign_RolesScored_BestFitChosen` — proves the scoring chain works

## Acceptance

- Save / Load round-trip: `Save` populates a fully-populated `HoNFormationPreset`; subsequent `Load` restores assignments to the same VM state.
- Auto-Assign: clicking Auto-Assign moves heroes into formations matching their `IHeroAutoAssigner.ScoreHeroForFormation` scores.
- Missing-hero pruning: a saved preset whose heroes were killed mid-campaign loads without crashing; missing entries are silently dropped.
- Reflection robustness: if `OrderOfBattleVM._allHeroes` is renamed in a future Bannerlord update, the feature degrades to "Phase-1 stub" mode instead of crashing.

## Out of scope (this issue)

- BattleActionBar stance enforcement (separate Phase-2 feature — requires firing-order / brace-pose APIs that v1.3.15 doesn't expose)
- `CompanionRoleService._cache` eviction on hero death (bounded leak, low priority)
