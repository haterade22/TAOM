# Migration Tracking

**Migration: Bannerlord 1.3.15 → 1.4.5 — Functional foundation complete (2026-05-22).**
**Status:** S0–S5b ✅ landed (adapters, GameModels, equipment XML migration, roster authoring). S6–S12 (smoke test, per-tier feature validation, Codex review, closeout) were rolled into ongoing feature work on the `bannerlord-1.4.5` branch rather than executed as discrete gates — see commit history from 2026-05-22 onward for de-facto validation (CC fixes, warg combat, faction-map UI overhaul, career system tooltips, etc.). The formal validation pipeline was not run; runtime exposure has come through feature development.

**Subsequent minor engine bumps (tracked engine, no schema migration):** v1.4.5 → v1.4.6 (spider/elephant native-crash line) → **v1.4.7 (2026-07-08, current)**. Each was handled via `/engine-bump` (preserve baseline → regen decompile → `/verify-bindings` → parity audits → snapshot refresh) rather than a fresh migration.

For detailed analysis see:
- [v1.4.7-impact.md](v1.4.7-impact.md) — **v1.4.7 changelog→surface→verdict impact matrix + code changes (current bump)**
- [v1.4.x-overview.md](v1.4.x-overview.md) — executive summary + session map
- [v1.4.x-changes.md](v1.4.x-changes.md) — full changelog analysis
- [v1.4.x-equipment-overhaul.md](v1.4.x-equipment-overhaul.md) — v1.4.3 equipment system deep dive
- [v1.4.x-taom-impact.md](v1.4.x-taom-impact.md) — per-surface impact matrix (1.3.15→1.4.5)
- [dual-dll-setup.md](dual-dll-setup.md) — Steam update + DLL backup procedure

Plan file: `C:\Users\mikew\.claude\plans\we-did-this-on-crystalline-piglet.md`

---

## v1.3.15 → v1.4.5 Migration Status (CURRENT)

**Branch:** `bannerlord-1.4.5` (created from `bannerlord-1.3.15` HEAD `2f6756d` on 2026-05-21).
**1.3.15 DLL backup:** `E:\BannerlordBackup\1.3.15-bin\Win64_Shipping_Client\` (1.475 GB, 8,568 files, Version.xml = v1.3.15 confirmed).
**Old decompile archived:** `E:\Decompiled_Bannerlord_v1.4_OLD\` (was stale 1.4.x dump).

### S0 — Foundation

| Task | Status | Owner |
|---|---|---|
| Read full v1.4.x changelog | ✅ Complete | 2026-05-21 |
| Create `bannerlord-1.4.5` branch | ✅ Complete | 2026-05-21 |
| Backup 1.3.15 DLLs to `E:\BannerlordBackup\1.3.15\bin\Win64_Shipping_Client\` | ✅ Complete | 2026-05-21 |
| Archive stale `E:\Decompiled_Bannerlord\` to `_v1.4_OLD\` | ✅ Complete | 2026-05-21 |
| Scaffold v1.4.x migration docs | ✅ Complete | 2026-05-21 |
| User: disable Steam auto-update | ✅ Complete | User, 2026-05-22 |
| User: Let Steam update Bannerlord to 1.4.5 | ✅ Complete | User, 2026-05-22 |
| Reorganize backup to standard layout (1.3.15-bin → 1.3.15/bin/Win64_Shipping_Client) | ✅ Complete | 2026-05-22 |
| Update `Directory.Build.props` for dual-DLL (BANNERLORD_OVERRIDE_DIR support) | ✅ Complete | 2026-05-22 |
| Bulk decompile 1.4.5 → `E:\Decompiled_Bannerlord\` (6,146 core + 354 module .cs files) | ✅ Complete | 2026-05-22 |
| Decompile SandBox + StoryMode modules → `E:\Decompiled_Bannerlord\Modules\` | ✅ Complete | 2026-05-22 |
| Make `tools/taom-src.ps1` version-auto-detecting (reads Version.xml) | ✅ Complete | 2026-05-22 |
| Write `tools/decompile_to_folder.ps1` (bulk ilspycmd wrapper) | ✅ Complete | 2026-05-22 |
| Write `tools/migrate_equipment_type_1_4_3.py` | ✅ Complete + revised (Battle implicit) | 2026-05-22 |
| Write `tools/audit_equipment_roster_coverage.py` | ✅ Complete | 2026-05-22 |
| Write `tools/validate_equipment_flags_1_4_3.py` | ✅ Complete | 2026-05-22 |
| TAOM Dependencies source location audit | ✅ Complete — lives in this repo at `Dependencies/TAOM.Dependencies.csproj` but de-tracked from git in commit 0b16cca. Restore via `git checkout 0b16cca -- Dependencies/`. | 2026-05-22 |
| Verify April-2026 fixes still apply (Alliance, BattleReward, SpecialResources) | ⚠️ DRIFT FOUND — see "Open issues" below | 2026-05-22 |
| Update `Main/_Module/SubModule.xml` Native dep version (e1.3.0.* → e1.4.5.*) | ✅ Complete | 2026-05-22 |
| Generate `docs/migration/api-diff-1.3.15-to-1.4.5.md` | ✅ Complete (15 classes, top 3 risks flagged) | 2026-05-22 |
| Audit `VerticalTopToBottom`/`VerticalBottomToTop` in TAOM prefabs | ✅ Complete — 5 prefab + 1 C# site found; may need swap | 2026-05-22 |
| Author per-XML-type template docs (`docs/migration/templates/`) from vanilla 1.4.5 | ✅ Complete — 4 docs (README + characters + equipment-rosters + troops-and-parties) | 2026-05-22 |
| Open GitHub tracking issue | ⏳ Pending | |

### S0 — Key findings (from parallel agents)

#### Equipment system (v1.4.3 overhaul)

- **3,372 `civilian="true"` occurrences** mapped to **2,017 in troop files + ~1,355 elsewhere**. Migration tool revised — `equipmentType="Battle"` is IMPLICIT in vanilla, do not add explicit Battle to bare sets.
- **Critical distinction surfaced:** `<EquipmentSet civilian="true">` is deprecated (zero vanilla 1.4.5 occurrences), but `<EquipmentRoster civilian="true">` inline inside `<NPCCharacter>/<Equipments>` is STILL valid (1,097 occurrences in vanilla `spnpccharacters.xml` alone). Migration tool correctly filters by `<EquipmentSet>` element only.
- **160 deprecated `EquipmentFlags` hits in `taom_child_equipment_templates.xml`** — `IsNoncombatantTemplate` (60), `IsNobleTemplate` (60), `IsCivilianTemplate` (40). Single file, manual review with vanilla template mapping.
- **`<Flags>` syntax confirmed** — single child element with per-flag boolean attributes (`<Flags IsLordTemplate="true" IsFemaleTemplate="true" />`), not multiple `<Flag>` children.
- **`IsNobleTemplate` is RENAMED to `IsLordTemplate` (1:1)**, not removed as dev notes implied.
- **All 12 TAOM custom cultures fail mandatory roster matrix** — expected pre-S5b authoring. The audit returns 0/96 mandatory + 0/48 optional rosters passing because current rosters use OLD flag names not in the new flag set.
- **Already 1.4.5-compatible (no migration needed):** `taom_career_starting_equipment.xml`, `taom_education_equipment_templates.xml`.
  - **Correction (2026-06-22):** "no migration needed" was scoped to flags/`equipmentType` only (education rosters correctly omit both). `taom_education_equipment_templates.xml` was in fact missing the required `culture="Culture.<id>"` on **all 980** `<EquipmentRoster>` elements → 980 `don't have culture definition` startup warnings. Fixed by adding the attribute in vanilla's two-attr layout (`tools/add_education_roster_cultures.py`). The `_<culture>` id suffix supplies the value; all 10 cultures verified in `taom_spcultures.xml`.

#### GameModel drift discovered

⚠️ **`TaomBattleRewardModel`** — current TAOM file on `bannerlord-1.4.5` branch has 3-param `CalculateRenownGain` signature. v1.4.5 base has 5 params (`PartyBase, float, float, float, bool`). Either the April-2026 fix was reverted or the wrong file was committed. **S3 must add the 2 missing params** (`renownMultiplierForWinnerSide`, `includeDescriptions`) and pass through to `base.`.

⚠️ **`TaomAllianceModel`** — opposite drift. Has the v1.4.0-fix `IFaction evaluatingFaction` param that 1.4.5 dropped. **S3 must drop that param** from the override signature. If the per-kingdom modifier needs the evaluating faction, look at `GetSupportScoreOfStartingAllianceForClan` (still has `Clan evaluatingClan`).

✅ **`SpecialResourcesBehavior.OnHideoutCompleted`** — signature still matches 1.4.5 (3-param shape from April-2026 fix is correct).

#### ChildCreatorAdapter critical rewrite

🔥 **`Main/Adapters/ChildCreatorAdapter.cs:40`** — vanilla method **renamed and signature changed**:
- 1.3.15: `MBList<MBEquipmentRoster> GetEquipmentRostersForInitialChildrenGeneration(Hero hero)`
- 1.4.5: `Equipment GetEquipmentForInitialChildrenGeneration(Hero hero)` (single Equipment, gender + culture filtering moved inside the model)

Rewrite recipe — replace the roster-loop with a direct `EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, civilianEquipment)` plus a derived battle equipment via `Equipment.FillFrom(...)`.

#### `OnRulingClanChanged` parameter flip — TAOM impact

TAOM doesn't subscribe directly. `Patch24_BannerDriftGuard` reads `__instance.Kingdom?.RulingClan` synchronously inside a vanilla method, not from the event — UNAFFECTED. `Patch12_WarOfTheRing` has zero `RulingClan/RulerClan/OnRulingClanChanged` references in `Main/Features/Diplomacy/**` — UNAFFECTED.

#### Other v1.4.5 surface

- **`DefaultAllianceModel` gained 5 new public methods** (`CanMakeAlliance`, `GetSupportScoreOfStartingAllianceForClan`, `GetAllianceFactorForDeclaringWar/Peace`, `GetProposerClanForAllianceDecision`). TAOM's `MaxNumberOfAlliances => int.MaxValue` may not be sufficient — `CanMakeAlliance` adds score-threshold + player-support gates that can independently veto. S3 needs to evaluate.
- **Naval DLC additions** in BattleReward / CombatSimulation / MilitaryPower / TargetScore models (new `Ship`/`Figurehead`/`IsTargetingPort` methods). Nothing breaks at compile time — TAOM doesn't override these — but vanilla will invoke them during any naval event. **Document naval as out-of-scope unless explicitly opted in.**
- **TaleWorlds 12 cultures, not 10** — `taom_spcultures.xml` enumeration: erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, umbar, dolguldur, gondor, mordor, shaghana, abanissa. CLAUDE.md's "10 custom cultures" line is stale.

#### Prefab UI fix (v1.4.0) — 6 TAOM sites

Vanilla 1.4.0 fixed inverted `VerticalTopToBottom` / `VerticalBottomToTop` ListPanel semantics. TAOM has 6 sites using `VerticalBottomToTop`:
- `Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs:24` (string-injected)
- `Main/_Module/GUI/Prefabs/FacGen/PreBuildCharacterSelection.xml` (lines 38, 40, 51, 59)

Visual order may now be inverted. S5 task: verify in-game; swap to `VerticalTopToBottom` if inverted.

### S1 — TAOM Dependencies (COMPLETE 2026-05-22)

| Task | Status |
|---|---|
| Restore Dependencies/ tree from git SHA `0b16cca` (1,444 files) | ✅ |
| Build TAOM.Dependencies.csproj against 1.4.5 | ✅ 0 errors, 878 benign warnings |
| Verify output deployed to `Dependencies/_Module/bin/{Win64,Gaming.Desktop.x64}_Shipping_Client/` | ✅ |
| Verify Steam install path reflects rebuild | ✅ — `E:\...\Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\TAOM.Dependencies.dll` synced |

**Conclusion:** The internalized Harmony 2.4.2 fork is fully API-compatible with Bannerlord 1.4.5. No Harmony/MCM/UIExtenderEx version bumps needed.

### S2 — Adapters (COMPLETE 2026-05-22)

| Task | Status |
|---|---|
| `ChildCreatorAdapter.cs` rewrite — `GetEquipmentRostersForInitialChildrenGeneration` (returned `MBList<MBEquipmentRoster>`) → `GetEquipmentForInitialChildrenGeneration` (returns single `Equipment`); roster-loop removed | ✅ |
| Remaining 95 adapters compile clean | ✅ |

### S3 — GameModels (COMPLETE 2026-05-22)

| Task | Status |
|---|---|
| `TaomBattleRewardModel.CalculateRenownGain` — added `renownMultiplierForWinnerSide` (float) + `includeDescriptions` (bool) params. **Behavior note:** vanilla bakes the multiplier into the `ExplainedNumber` base value, so TAOM's `ApplyRenownFeats` and career `ApplyFactor` (both `AddFactor` calls) scale proportionally with it. Consistent with vanilla perk scaling. | ✅ |
| `TaomAllianceModel.GetScoreOfStartingAlliance` — dropped `IFaction evaluatingFaction` param | ✅ |
| Remaining 36 GameModels compile clean | ✅ |
| **Behavior gate: army/diplomacy reworks did NOT break our overrides** | ✅ at compile; ⏳ runtime |

### S4 — Harmony patches (COMPILE-CLEAN, runtime pending S6)

| Task | Status |
|---|---|
| 70 Harmony patches compile clean against 1.4.5 | ✅ |
| Runtime binding verification (target methods exist) | ✅ **offline gate** — `TAOM.Tests/Migration/HarmonyPatchBindingTests` resolves all 110 patch targets against the installed v1.4.5 engine on every `dotnet test`. Caught + fixed a real ambiguity defect in `HeroViewModel_FillFrom_Patch` on first run (2026-05-28). In-game patch *application* still on the [S6 punch-list](./s6-runtime-punchlist.md). |

### S5 — Mixin / PrefabExtension (COMPILE-CLEAN, runtime pending S6)

| Task | Status |
|---|---|
| 8 Mixin/Prefab surfaces compile clean | ✅ |
| `VerticalBottomToTop` swap in 6 sites (v1.4.0 fixed inversion) | ⏳ S6 visual check; swap if inverted |

### S5+ bonus fix

| Task | Status |
|---|---|
| `SpecialResourcesBehavior.OnHideoutCompleted` — added 3rd param `HideoutEventComponent.HideoutBattleEndState` (v1.4.3 event signature change). **Accepted-permissive deferral:** TAOM earns the resource for any `winnerSide == Attacker` outcome regardless of `battleEndState` value (None, Retreated, Defeated, Victory, SendTroops). This preserves v1.3.15 behavior. If S7 feature validation reveals it's too permissive, gate on `battleEndState == Victory`. | ✅ |

### Build + test gate

| Gate | Result |
|---|---|
| `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` | ✅ **0 errors, 1 warning** |
| `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true` | ✅ **2,323 / 2,325 pass (2 skipped, 0 failed)** |
| Total C# fixes | **4 files** (TaomBattleRewardModel, TaomAllianceModel, ChildCreatorAdapter, SpecialResourcesBehavior) |
| Predicted vs actual scope | Predicted ~96 adapter audits, 38 GameModel audits, 70 Harmony patch audits — **actual: 4 fixes total** |

### S5a — Mass XML migration (COMPLETE 2026-05-22)

| Task | Status |
|---|---|
| Migrate `<EquipmentSet civilian="true">` → `equipmentType="Civilian"` across 16 XML files (1,628 occurrences) | ✅ |
| Migrate same pattern in `lords.xslt` (389 occurrences) | ✅ |
| Manually rewrite `taom_child_equipment_templates.xml` — 60 IsNobleTemplate renames + 40 IsCivilianTemplate drops + 60 IsNoncombatantTemplate drops (total 160) | ✅ |
| Update 8 Python XML generators to emit new format | ✅ |
| Validation: `validate_equipment_flags_1_4_3.py` returns 0 hits | ✅ |
| Validation: migrate dry-run reports 0 files needing changes | ✅ |
| Build + test gate: `dotnet build` 0 errors + `dotnet test` 2,323/2,325 pass | ✅ |
| Migration tool revised to use regex-on-text (preserves all formatting; 130K-line lxml-write reformat reverted) | ✅ |

**Diff summary:** 26 files modified, ~2,150 line changes total. All formatting preserved. The deprecated `civilian="true"` count went from 3,372 (raw grep including legitimate inline `<EquipmentRoster civilian="true">`) to 2,017 actual migrations (1,628 EquipmentSet + 389 XSLT). The 1,097 inline `<EquipmentRoster civilian="true">` remain untouched per vanilla 1.4.5 convention.

### S5b — Equipment roster authoring (COMPLETE 2026-05-22)

| Task | Status |
|---|---|
| Generate 76 mandatory rosters (12 cultures × {6,8} combos) via new `tools/generate_lord_template_equipment.py` | ✅ |
| Write to new file `Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml` (additive — does not modify existing files) | ✅ |
| Register in `Main/_Module/SubModule.xml` `<Xmls>` block | ✅ |
| Coverage audit: all 12 cultures pass 8/8 mandatory combos | ✅ |
| Build + test gate green | ✅ |

**Approach:** the generator extracts items from each culture's existing `<EquipmentRoster id="<culture>_bat_template_*">` and `<culture>_civ_template_*">` rosters and emits 6 new rosters per culture with the right `<Flags>` combinations. Shaghana + abanissa (no per-culture equipment files; Harad sub-cultures per `kingdom-culture-mapping.md` memory) fall back to harad items + also get 2 extra rosters (child M/F) since they aren't covered by `taom_child_equipment_templates.xml`.

**Deferred (4 of 12 optional combos per culture):** `IsKingdomRulerTemplate` × {male, female} × {Battle, Civilian}. The engine should fall back to `IsLordTemplate` rosters when no ruler-specific roster exists — verify in S6/S7. If the fallback isn't adequate, author dedicated ruler equipment in a future pass.

### S6–S12 — see plan file

**Note (2026-05-25):** Formal stages skipped. The build is green, tests pass (2,419/2 skip), and 1.4.5 feature work has been shipping on this branch since 2026-05-22 (warg combat, CC layout, faction-map overhaul, career tooltips, etc.). Treat any in-game crash or Harmony binding failure as a one-off `/investigate`, not a migration-stage gate.

S6 was the smoke-test gate; S7-S10 were feature validation; S11 was Codex; S12 was closeout.

**Update (2026-05-28): S6's offline-checkable portion is now a standing test gate.** `TAOM.Tests/Migration/` verifies — on every `dotnet test` — that all 110 Harmony patch targets bind, all 39 GameModels are registered + override correctly, and 32 auxiliary reflection members resolve against the *installed* v1.4.5 engine. It caught a real defect on first run (`HeroViewModel_FillFrom_Patch` ambiguity). The residual in-game checks (patch *application*, prefab visual order, ruler equipment, alliances, naval, dynamic-reflection CC flow) are itemized in [`s6-runtime-punchlist.md`](./s6-runtime-punchlist.md). A committed v1.4.5 signature snapshot lives at [`docs/reference/taleworlds-api-snapshot/`](../reference/taleworlds-api-snapshot/) so signature lookups no longer require the external decompile dump.

### Risk surface inventory (verified)

| Surface | Count |
|---|---|
| TAOM features under `Main/Features/` | 45 |
| Harmony patches | 70 (all attribute-based) |
| GameModel overrides | 38 |
| Reflection / private-API call sites | 79 (across 44 files) |
| Adapters | 96 |
| Mixin / UIExtenderEx surfaces | 8 |
| Test files | 189 |
| `civilian="true"` XML occurrences | 3,372 (across 51 files) |
| Old `EquipmentFlags` references | 1 file (`taom_child_equipment_templates.xml`) |
| `civilianTemplate`/`battleTemplate` attrs | 0 files ✓ |
| Direct `OnRulingClanChanged` subscribers | 0 ✓ |
| Critical adapter rewrites | 1 (`ChildCreatorAdapter`) |

### Open issues (S0 surfaced, S2+ acts)

| Issue | Owner | Severity |
|---|---|---|
| `TaomBattleRewardModel.CalculateRenownGain` has 3-param signature; v1.4.5 needs 5 (add `renownMultiplierForWinnerSide`, `includeDescriptions`) | S3 | 🔥 won't bind in 1.4.5 |
| `TaomAllianceModel.GetScoreOfStartingAlliance` has extra `IFaction evaluatingFaction` param (1.4.0 fix); v1.4.5 dropped it again | S3 | 🔥 won't bind in 1.4.5 |
| `ChildCreatorAdapter.cs:40` API renamed + return type changed (list → single Equipment); structural rewrite needed | S2 | 🔥 won't compile |
| `DefaultAllianceModel` has 5 new public methods; `CanMakeAlliance` may veto despite `MaxNumberOfAlliances=int.MaxValue` | S3 | 🟡 feature break risk |
| `taom_child_equipment_templates.xml` uses 160 deprecated `EquipmentFlags` values | S5a | 🟡 single-file manual review |
| `taom_spcultures.xml` defines 12 cultures, but no `<Flags>` elements in 15 culture roster files — engine cannot resolve them by template | S5b | 🔥 missing rosters → naked NPCs |
| 6 TAOM sites use `VerticalBottomToTop` ListPanel layout — v1.4.0 fixed inversion; visual order may be wrong now | S5 | 🟡 cosmetic but possible |
| TAOM.Dependencies project source is de-tracked from git (since 0b16cca, April 2026); needs restore via `git checkout 0b16cca -- Dependencies/` | S1 | 🟡 blocks Dependencies rebuild |
| GitHub tracking issue not yet opened | S0 (final) | 🟢 process |

### Resolved open questions (from S0)

| Question | Answer |
|---|---|
| Where does TAOM.Dependencies project source live? | In this repo at `Dependencies/TAOM.Dependencies.csproj`, but de-tracked from git in commit `0b16cca` (April 2026). Last-known-good csproj is recoverable via `git show 0b16cca:Dependencies/TAOM.Dependencies.csproj`. Bundles Harmony 2.4.2 internalized fork + polyfills. |
| How many cultures does TAOM have? | 12 (not 10): erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, umbar, dolguldur, gondor, mordor, shaghana, abanissa. CLAUDE.md is stale. |
| Do any TAOM prefabs use `VerticalTopToBottom` / `VerticalBottomToTop`? | Yes — 6 sites (5 in `PreBuildCharacterSelection.xml`, 1 string-injected in `MessengerEncyclopediaPrefabExtension.cs`). All use `VerticalBottomToTop`. May need swap to `VerticalTopToBottom` after S6 smoke test. |
| Is `equipmentType="Battle"` implicit or required? | IMPLICIT. Vanilla 1.4.5 omits the attribute on battle sets. Only `Civilian` and `Stealth` are explicit. Migration tool revised accordingly. |
| Are `<EquipmentRoster civilian="true">` inline rosters also deprecated? | NO. Vanilla 1.4.5 still uses 1,097× in `spnpccharacters.xml`. Only `<EquipmentSet civilian="true">` is deprecated. |
| Is `IsNobleTemplate` removed or renamed? | RENAMED 1:1 to `IsLordTemplate`. Dev migration notes mislabeled as "removed". |
| Vanilla module Native dep version format? | TAOM uses BUTR schema `version="e1.4.5.*"`. Vanilla uses `DependentVersion="v1.4.5"` (different schema, same purpose). TAOM stays on BUTR since SubModule.xml references BUTR XmlSchema. |

---

## Historical: v1.2.12 → v1.3.12 Migration (COMPLETE)

Tracker preserved from prior migration. **Last updated 2026-04-02.**

## Overall Progress

| Category | Total | Complete | Remaining |
|----------|-------|----------|-----------|
| Module XML | 1 | 1 | 0 |
| XSLT Transformations | 5 | 5 | 0 |
| Culture XML | 1 | 1 | 0 |
| Kingdom XML | 1 | 1 | 0 |
| Clan XML | 1 | 1 | 0 |
| Lords XML | 1 | 1 | 0 |
| Heroes XML | 1 | 1 | 0 |
| Settlement XML | 1 | 0 | 1 |
| Troop XML | 2 | 0 | 2 |
| LOTRAOM Troop Files | 13 | 13 | 0 |
| Item XML | 1 | 0 | 1 |
| Equipment XML | 1 | 0 | 1 |
| Code Changes | TBD | 0 | TBD |

---

## Module XML

| File | Status | Notes |
|------|--------|-------|
| `SubModule.xml` | COMPLETE | Updated with XSLT entries for kingdoms, cultures, clans, lords |

---

## XSLT Transformations

TAOM uses XSLT transformations to modify vanilla XML at load time, renaming entities with LOTR-themed names while preserving vanilla structure.

| File | Status | Transforms | Notes |
|------|--------|------------|-------|
| `spkingdoms.xslt` | COMPLETE | 8 kingdoms | Dunland, Gondor, Mordor, Dale, Harad, Rohan, Khand, Rhûn |
| `spcultures.xslt` | COMPLETE | 6 cultures | Dunlending, Barding, Haradrim, Rohirrim, Variag, Easterling |
| `spclans.xslt` | COMPLETE | 73 clans | All noble clans across 8 kingdoms |
| `lords.xslt` | COMPLETE | 380 lords | Consolidated templates with name, default_group, is_female, BodyProperties, skills, traits |
| `heroes.xslt` | COMPLETE | 415 heroes | Biographical text for all heroes |

### Lords XSLT Structure (Refactored)

Each lord template now contains ALL transformations in one place:
- `name` attribute with LOTRAOM name
- `default_group` attribute (Infantry, Cavalry, HorseArcher, etc.)
- `is_female` attribute where applicable
- `face/BodyProperties` with weight, build, and key
- Complete `skills` section (16 skills)
- Complete `Traits` section (personality and political traits)

### XSLT Mapping Reference

| Vanilla Kingdom | TAOM Name | Vanilla Culture | TAOM Culture |
|-----------------|-----------|-----------------|--------------|
| empire | Dunland | empire | Dunlending |
| empire_w | Gondor | - | - |
| empire_s | Mordor | - | - |
| sturgia | Dale | sturgia | Barding |
| aserai | Harad | aserai | Haradrim |
| vlandia | Rohan | vlandia | Rohirrim |
| battania | Khand | battania | Variag |
| khuzait | Rhûn | khuzait | Easterling |

---

## Culture XML

| File | Status | Notes |
|------|--------|-------|
| `spcultures.xslt` | COMPLETE | Renames 6 main cultures via XSLT transformation |

### Culture Schema Changes (1.2 → 1.3)
- [x] Verify `default_face_key` format - unchanged
- [x] Check new required attributes - `default_stealth_equipment_roster` now required
- [x] Update culture bonuses format - unchanged
- [x] Verify troop tree references - unchanged

---

## Settlement XML

| File | Status | Notes |
|------|--------|-------|
| `settlements.xml` | NOT STARTED | Settlements and castles |

### Settlement Schema Changes (1.2 → 1.3)
- [ ] Check component structure changes
- [ ] Verify bound village references
- [ ] Update prosperity/loyalty defaults
- [ ] Check workshop/production changes

---

## Kingdom XML

| File | Status | Notes |
|------|--------|-------|
| `spkingdoms.xslt` | COMPLETE | Renames 8 kingdoms via XSLT transformation |

### Kingdom Schema Changes (1.2 → 1.3)
- [x] `initial_home_settlement` now REQUIRED
- [x] `label_color` deprecated but still works
- [x] `alternative_color` deprecated but still works
- [x] `alternative_color2` deprecated but still works

---

## Clan XML

| File | Status | Notes |
|------|--------|-------|
| `spclans.xslt` | COMPLETE | Renames 73 noble clans via XSLT transformation |

### Clan Schema Changes (1.2 → 1.3)
- [x] No significant schema changes detected
- [x] Backward compatible with 1.2 format

---

## Additional Clans (New LOTRAOM Clans)

Clans that exist in LOTRAOM but NOT in vanilla Bannerlord are added via direct XML (not XSLT).

| File | Status | Count | Notes |
|------|--------|-------|-------|
| `characters/clans.xml` | COMPLETE | ~101 clans | Extended kingdom clans, Middle-Earth factions, minor factions, bandits |

### Clan Breakdown

| Category | Count | Details |
|----------|-------|---------|
| Extended Kingdom Clans | 39 | Empire West 10-18, Empire South 10-18, Vlandia 12-22, Khuzait 10-19 |
| Middle-Earth Factions | 39 | Erebor, Rivendell, Mirkwood, Lothlorien, Isengard, Gundabad, Umbar, Dol Guldur |
| Minor Factions | 14 | Including forest_people (Shadowkin), eleftheroi (Guardians of Tharbad) |
| Bandit Clans | 9 | Including gondor_bandits, pale_uruk_bandits |

### Extended Kingdom Clan IDs

| Kingdom | TAOM Name | Clan IDs | Count |
|---------|-----------|----------|-------|
| empire_w | Gondor | clan_empire_west_10 through 18 | 9 |
| empire_s | Mordor | clan_empire_south_10 through 18 | 9 |
| vlandia | Rohan | clan_vlandia_12 through 22 | 11 |
| khuzait | Rhun | clan_khuzait_10 through 19 | 10 |

*Note: Vanilla clans 1-9 (or 1-11 for Vlandia) are handled by `spclans.xslt`. Extended clans 10+ are NEW in LOTRAOM and defined in `clans.xml`.*

---

## Heroes XML

| File | Status | Notes |
|------|--------|-------|
| `spheroes.xslt` | COMPLETE | Adds LOTR-themed biographical text for all 415 heroes |

### Heroes Schema Notes
- [x] Heroes.xml contains family relationships (spouse, father, mother)
- [x] Heroes.xml contains faction references
- [x] `text` attribute holds biographical descriptions
- [x] Hero IDs match lord IDs (lord_X_Y pattern)
- [x] `heroes.xslt` now applies family relationships from LOTRAOM data

---

## Additional Lords (New LOTRAOM Lords)

Lords that exist in LOTRAOM but NOT in vanilla Bannerlord are added via direct XML (not XSLT).

| File | Status | Count | Notes |
|------|--------|-------|-------|
| `characters/lords.xml` | COMPLETE | 504 lords | New LOTRAOM lords not in vanilla |

These lords include custom cultures: gondor, mordor, erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, umbar, dolguldur.

---

## Troop XML

| File | Status | Notes |
|------|--------|-------|
| `spnpccharacters.xml` | NOT STARTED | Non-lord characters |
| `lords.xslt` | COMPLETE | Transforms 380 vanilla lords with LOTRAOM data |

### Troop Schema Changes (1.2 → 1.3)
- [x] `BodyProperties version` changed from 3 to 4
- [x] `preferred_upgrade_formation` attribute added (optional)
- [ ] Verify skill format changes - pending for spnpccharacters
- [ ] Check equipment set references - pending for spnpccharacters

---

## LOTRAOM Troop Files

Copied from `E:/LOTRAOMAssets/LOTRAOM_Jan_1_Patreon/Modules/LOTRAOM/ModuleData/` to `Main/_Module/ModuleData/troops/`.

**Status:** STAGED (copied but NOT registered in SubModule.xml)

| File | Status | Lines | Culture | Notes |
|------|--------|-------|---------|-------|
| `troops_rohan.xml` | STAGED | 5,803 | vlandia | Horse archers, Riders of Rohan |
| `troops_gondor.xml` | STAGED | 6,955 | gondor* | Tower Guard, Knights, Rangers |
| `troops_harad.xml` | STAGED | 1,911 | aserai | Haradrim warriors |
| `troops_mordor.xml` | STAGED | 4,300 | custom* | Orcs, Trolls, Black Númenóreans |
| `troops_isengard.xml` | STAGED | 5,045 | custom* | Uruk-hai |
| `troops_rhun.xml` | STAGED | 2,041 | khuzait | Easterling warriors |
| `troops_dunland.xml` | STAGED | 3,137 | empire | Dunlending hillmen |
| `troops_erebor.xml` | STAGED | 6,249 | custom* | Dwarves of Erebor |
| `troops_rivendell.xml` | STAGED | 7,169 | custom* | Elves of Rivendell |
| `troops_mirkwood.xml` | STAGED | 951 | custom* | Wood Elves, Spiders |
| `troops_gundabad.xml` | STAGED | 1,943 | custom* | Goblins, Orcs |
| `troops_umbar.xml` | STAGED | 2,139 | custom* | Corsairs of Umbar |
| `troops_dolguldur.xml` | STAGED | 7,622 | custom* | Necromancer forces |

*\*Custom cultures require new culture definitions before activation*

### Schema Verification
- [x] No `BodyProperties version` attributes (uses `face_key_template` references)
- [x] XML well-formed
- [ ] Item references need verification (custom LOTRAOM items)
- [ ] Culture references need custom culture definitions

### To Activate These Troops
1. Create custom culture XMLs for non-vanilla cultures (gondor, mordor, erebor, etc.)
2. Add item XMLs for referenced custom equipment
3. Register troop files in `SubModule.xml` with `<XmlNode>` entries
4. Update culture `basic_troop`, `elite_basic_troop`, etc. references

---

## Item XML

| File | Status | Notes |
|------|--------|-------|
| `spitems.xml` | NOT STARTED | Weapons, armor, items |

### Item Schema Changes (1.2 → 1.3)
- [ ] Check weapon stats format
- [ ] Verify armor tier system
- [ ] Update crafting data format
- [ ] Check item culture assignments

---

## Equipment XML

| File | Status | Notes |
|------|--------|-------|
| `equipment_sets.xml` | NOT STARTED | Equipment loadouts |

### Equipment Schema Changes (1.2 → 1.3)
- [ ] Verify equipment slot naming
- [ ] Check civilian/battle equipment
- [ ] Update equipment pool references

---

## Code Changes

| Component | Status | Notes |
|-----------|--------|-------|
| Harmony Patches | NOT STARTED | Verify method signatures |
| GameModels | NOT STARTED | Check overridden methods |
| CampaignBehaviors | NOT STARTED | Event signature changes |
| MissionLogics | PARTIAL | See 1.3.12 API discoveries below |

### Known API Changes (1.2 → 1.3.12)
- [ ] `Mission` constructor parameters
- [ ] Campaign event delegates
- [ ] Party management methods
- [ ] Settlement query methods

### Discovered API Changes (from Warg Combat Port, #44 / #47)

| Change | 1.2 | 1.3.12 | Impact |
|--------|-----|--------|--------|
| `OnBehaviorInitialize` | Called for all behaviors | **NOT called** for behaviors added in `OnMissionBehaviorInitialize` | Use constructor or first-tick init instead |
| `Mission.RegisterBlow` param 3 | `GameEntity` | `WeakGameEntity` | Reflection lookup returns null if wrong type |
| `Agent.AgentVisuals` return type | `AgentVisuals` | `MBAgentVisuals` | Different namespace (`View` vs `MountAndBlade`) |
| `OnMainAgentChangedDelegate` | `(object, PropertyChangedEventArgs)` | `(Agent oldAgent)` | Single parameter, no event pattern |
| `CombatLogData` ctor param 13 | `bool` | `MissionObject` | Pass `null` instead of `false` |
| `AIScriptedFrameFlags` | Top-level enum | Nested in `Agent` class | Use `Agent.AIScriptedFrameFlags.None` |

---

## Testing Checklist

### Basic Loading
- [ ] Mod loads without errors
- [ ] No missing XML element warnings
- [ ] No null reference on startup

### Campaign
- [ ] New campaign starts successfully
- [ ] Cultures display correctly
- [ ] Settlements appear on map
- [ ] Lords spawn with correct equipment

### Gameplay
- [ ] Combat functions normally
- [ ] Troop upgrades work
- [ ] Economy systems function
- [ ] AI behaves correctly

---

## Bannerlord 1.3.15 (BannerlordTogether Requirement)

BannerlordTogether v0.2.2 requires `DependentVersion="v1.3.15.110062"`. TAOM's SubModule.xml declares `v1.3.12` but the mod is **confirmed running on 1.3.15 in singleplayer** as of 2026-04-02 with no observed runtime failures.

| Area | Status | Notes |
|------|--------|-------|
| Singleplayer runtime | CONFIRMED OK | User running 1.3.15 with no issues |
| Harmony patch signatures | ASSUMED OK | All patches functional in game |
| GameModel overrides | ASSUMED OK | No reported failures |
| SubModule.xml version declaration | LOW PRIORITY | Can update to `v1.3.15` when ready |
| API delta 1.3.12 → 1.3.15 | NOT AUDITED | No formal diff done; no issues reported |

**Recommendation:** Update `SubModule.xml` `DependentVersion` from `v1.3.12` to `v1.3.15` in a future housekeeping PR to reflect actual runtime target.

---

## Notes

### Migration Session Log

**2026-01-28 (Session 9)**: Clan migration completion:
- Removed 18 placeholder `clan_taom_*` clans from `characters/clans.xml`
- Added 39 extended kingdom clans: Empire West 10-18, Empire South 10-18, Vlandia 12-22, Khuzait 10-19
- Added 2 missing minor factions: `forest_people` (Shadowkin), `eleftheroi` (Guardians of Tharbad)
- All clan definitions sourced from LOTRAOM `LOTRAOM_spclans.xml` with matching IDs and names
- GitHub Issue #1 created and closed documenting the change
- Final clans.xml count: ~101 clans (39 extended + 39 Middle-Earth + 14 minor factions + 9 bandits)

**2026-01-25 (Session 8)**: Lords skill templates:
- Added `skill_template` attribute to all 504 custom lords in `characters/lords.xml`
- Created PowerShell script `tools/oneoff/lords-migration/add-skill-templates.ps1` for batch updates
- Random variety approach: Multiple template options per category (e.g., Infantry can get shock_troop, phalanx, berserker, or swordsman)
- Rookie variants assigned to lords under age 25
- Template distribution: 25 unique templates used across lords
- Build verified successful

**2026-01-25 (Session 7)**: Lords face tags:
- Added `hair_tags`, `beard_tags`, and `tattoo_tags` to all 504 custom lords in `characters/lords.xml`
- Created PowerShell script `tools/oneoff/lords-migration/add-face-tags.ps1` for batch updates
- Culture-appropriate tags assigned (e.g., rivendell → battania, dolguldur → empire)
- Female lords correctly exclude `beard_tags`
- Build verified successful

**2026-01-24 (Session 6)**: Heroes XSLT family relationships:
- Updated `heroes.xslt` to include `spouse`, `father`, and `mother` attributes for all heroes
- Extracted family relationship data from LOTRAOM `heroes.xml`
- Added bidirectional spouse relationships (both partners reference each other)
- Added parent-child relationships (children reference father/mother)
- Updated templates for all 6 kingdoms: Empire (Dunland/Gondor/Mordor), Sturgia (Dale), Aserai (Harad), Vlandia (Rohan), Battania (Khand), Khuzait (Rhun)
- Also updated dead lords with family attributes where applicable
- Build verified successful

**2026-01-24 (Session 5)**: Lords XSLT refactoring and new lords:
- Refactored `lords.xslt` with consolidated templates (380 vanilla lords)
- Each lord template now includes: name, default_group, is_female, BodyProperties, skills, traits
- Created `characters/lords.xml` with 504 new LOTRAOM lords not in vanilla
- Renamed `splords.xslt` → `lords.xslt` and `spheroes.xslt` → `heroes.xslt` for clarity
- Updated SubModule.xml paths accordingly
- Fixed Mouth of Sauron (lord_1_14) gender: is_female="false"

**2026-01-24 (Session 4)**: Migrated LOTRAOM troop files:
- Copied 13 troops_*.xml files (~55,265 lines total) to `Main/_Module/ModuleData/troops/`
- Files staged but NOT registered in SubModule.xml (per request)
- No BodyProperties version updates needed (files use face_key_template references)
- Build verified successful

**2026-01-24 (Session 3)**: Completed Heroes biographical XSLT transformation:
- Created `spheroes.xslt` with biographical text for all 415 heroes
- LOTR-themed descriptions for clan leaders, spouses, heirs, and dead lords
- Updated `SubModule.xml` with Heroes XmlNode entry

**2026-01-24 (Session 2)**: Completed XSLT transformations for all entity types:
- Created `spclans.xslt` with 73 clan name transformations
- Created `splords.xslt` with ~350 lord name transformations across all 6 kingdoms
- Updated `SubModule.xml` with XmlNode entries for clans and lords
- Created `XML-SCHEMA-CHANGES.md` documenting 1.2→1.3 schema differences
- Build verified successful

**2026-01-24 (Session 1)**: Initial tracking setup. Created XSLT transformations for kingdoms and cultures:
- Created `spkingdoms.xslt` with 8 kingdom transformations
- Created `spcultures.xslt` with 6 culture transformations
- Updated `SubModule.xml` with XSLT entries

---

## How to Update This File

1. Change status from `NOT STARTED` → `IN PROGRESS` → `COMPLETE`
2. Add notes about issues encountered
3. Check off subtask checkboxes as complete
4. Update "Last Updated" date at top
5. Add session notes in the log section

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/migration/README.md](./README.md)
- [docs/migration/s6-runtime-punchlist.md](./s6-runtime-punchlist.md)
- [docs/migration/templates/README.md](templates/README.md)
- [docs/migration/v1.4.x-overview.md](./v1.4.x-overview.md)
- [docs/reference/taleworlds-api-snapshot/README.md](../reference/taleworlds-api-snapshot/README.md)
- [docs/reference/taleworlds-api-snapshot/reflection-sites.md](../reference/taleworlds-api-snapshot/reflection-sites.md)

<!-- backlinks-end -->
