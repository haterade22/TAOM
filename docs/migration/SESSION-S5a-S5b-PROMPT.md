# Session prompt — S5a + S5b (v1.4.5 XML migration + equipment roster authoring)

Copy this entire prompt into a fresh Claude Code conversation. The session is fully independent of the current C# migration session — it touches only XML/data files and migration tooling. No C# code, no GameModel/Harmony/adapter changes.

---

## You are continuing the TAOM v1.3.15 → v1.4.5 migration

Branch: `bannerlord-1.4.5`. The C# code migration (S0–S5) is complete in a parallel session — build is green, 2,323/2,325 tests pass. **This session does the XML data migration** that the v1.4.3 equipment-system overhaul requires.

**Two sub-phases (run sequentially in this session):**
- **S5a** — mechanical migration of 3,372 `<EquipmentSet civilian="true">` → `equipmentType="Civilian"` across 51 files + manual rewrite of 160 deprecated `EquipmentFlags` references in 1 file.
- **S5b** — author ~96 new equipment rosters across TAOM's 12 custom cultures to satisfy v1.4.3's mandatory roster contract (`IsLordTemplate`, `IsKingdomRulerTemplate`, `IsFemaleTemplate`, `IsChildEquipmentTemplate`, `IsTeenagerEquipmentTemplate` combinations).

S5b depends on S5a — author against the new XML format, not the deprecated one. Do S5a fully before starting S5b.

## READ FIRST — required context (in order)

1. `docs/migration/v1.4.x-overview.md` — migration session map + scope
2. `docs/migration/v1.4.x-equipment-overhaul.md` — the v1.4.3 spec, deprecated → new flag mapping table, mandatory per-culture matrix
3. `docs/migration/templates/README.md` — index of "what right looks like" templates
4. `docs/migration/templates/equipment-rosters.md` — canonical reference for 1.4.5 equipment roster shape (vanilla examples + TAOM excerpts + diff)
5. `docs/migration/templates/characters.md` — character XML (lords, heroes, wanderers, NPCs) reference
6. `docs/migration/TRACKING.md` — current status. S0–S5 marked complete. S5a + S5b are your task. **Update this file as you finish each sub-phase.**

## Worktree recommendation (parallel-session safety)

The C# session at the main working tree may still have uncommitted changes. Avoid conflicts by creating a separate git worktree for S5a/S5b work:

```powershell
cd c:\Users\mikew\source\repos\TAOM
git worktree add ../taom-s5ab bannerlord-1.4.5
cd ../taom-s5ab
```

Do all S5a/S5b work in the worktree. Commit + push from there. The main session can independently land its commit.

If `git worktree add` reports the branch is checked out elsewhere, that's the main session — use `git worktree add ../taom-s5ab -b bannerlord-1.4.5-data-migration bannerlord-1.4.5` to make a side branch, then PR/merge back when both sessions are done.

## S5a — Mass XML migration

### Tools (all pre-built in S0)

| Tool | Purpose |
|---|---|
| `tools/migrate_equipment_type_1_4_3.py` | Mechanical `<EquipmentSet civilian="true">` → `equipmentType="Civilian"` migration. Only touches `<EquipmentSet>` elements. Leaves `<EquipmentRoster civilian="true">` alone (still valid in 1.4.5 — vanilla uses 1,097× in `spnpccharacters.xml`). Revised post-2026-05-22: does NOT add explicit `equipmentType="Battle"` to bare sets (vanilla 1.4.5 leaves Battle implicit). |
| `tools/audit_equipment_roster_coverage.py` | Produces per-culture coverage matrix CSV at `docs/migration/equipment-roster-coverage.csv`. |
| `tools/validate_equipment_flags_1_4_3.py` | Scans for deprecated `EquipmentFlags` names. Exit 0 = clean, exit 1 = hits found. |

### Procedure

1. **Pre-migration baseline:**
   ```powershell
   python tools/migrate_equipment_type_1_4_3.py --dry-run
   python tools/validate_equipment_flags_1_4_3.py
   python tools/audit_equipment_roster_coverage.py
   ```
   Capture the output. Expect ~3,372 `<EquipmentSet>` migrations across ~51 files, 160 deprecated flag hits in `taom_child_equipment_templates.xml`, all 12 cultures failing the mandatory matrix.

2. **Apply mechanical migration:**
   ```powershell
   python tools/migrate_equipment_type_1_4_3.py --apply
   ```
   This rewrites 51 XML files in-place. Verify with `git diff --stat -- Main/_Module/ModuleData/`.

3. **Spot-check the diff** in 3 representative files:
   - `Main/_Module/ModuleData/troops/troops_gondor.xml` (troops with inline rosters)
   - `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_gondor.xml` (per-culture equipment file)
   - `Main/_Module/ModuleData/lords.xslt:389` (XSLT passthrough)
   
   Confirm: only `<EquipmentSet civilian="true">` lines changed, `<EquipmentRoster civilian="true">` lines untouched, bare `<EquipmentSet>` elements left bare.

4. **Manual rewrite of `taom_child_equipment_templates.xml`:**
   The file has 160 deprecated `EquipmentFlags` hits across ~20 rosters using `IsNobleTemplate="true"` (60), `IsCivilianTemplate="true"` (40), `IsNoncombatantTemplate="true"` (60). Per the v1.4.3 spec:
   - `IsNobleTemplate` → `IsLordTemplate` (1:1 rename)
   - `IsCivilianTemplate` → drop flag, set `equipmentType="Civilian"` on the `<EquipmentSet>` inside
   - `IsNoncombatantTemplate` → drop flag, set `equipmentType="Civilian"`
   
   The full mapping is in `docs/migration/templates/equipment-rosters.md` under "Deprecated → new flag mapping."
   
   ⚠️ Pay attention to the `<Flags>` syntax: it's ONE element with per-flag attributes, not multiple `<Flag>` children: `<Flags IsLordTemplate="true" IsFemaleTemplate="true" />`.

5. **Update Python tooling that GENERATES TAOM XML** to emit the new format (so future regen doesn't bring back deprecated patterns). Files to update:
   - `scripts/replace_equipment_templates.py`
   - `tools/assign_xslt_lord_equipment.py`, `tools/assign_lord_equipment.py`
   - `tools/generate_rhun_troops.py`, `tools/generate_gondor_troops.py`
   - `tools/generate_char_creation_equipment.py`, `tools/generate_batch2_wanderers.py`
   - `tools/extract_wanderers.py`
   
   Grep each for `civilian="true"` and the deprecated flag names; replace with the new conventions.

6. **Validation gate:**
   ```powershell
   python tools/validate_equipment_flags_1_4_3.py
   # Must return 0 hits (exit code 0).
   
   python tools/migrate_equipment_type_1_4_3.py --dry-run
   # Must report 0 files needing migration.
   ```

7. **Build + test gate** (verify XML changes didn't break C# tests via embedded resource references):
   ```powershell
   dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true
   # Must be 0 errors.
   
   dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true
   # Must be 2,323/2,325 (same baseline as the C# session).
   ```

8. **Commit S5a:**
   ```
   migration(s5a): equipmentType migration + child template flag rewrite

   Per v1.4.3 spec — converted N <EquipmentSet civilian="true"> to
   equipmentType="Civilian" across 51 files (mechanical via tools/migrate_equipment_type_1_4_3.py).
   Manually rewrote 160 deprecated EquipmentFlags references in
   taom_child_equipment_templates.xml (IsNobleTemplate→IsLordTemplate rename,
   IsCivilianTemplate/IsNoncombatantTemplate dropped and replaced with
   equipmentType="Civilian" on the <EquipmentSet>).

   Inline <EquipmentRoster civilian="true"> blocks LEFT ALONE per vanilla 1.4.5
   precedent (1,097 occurrences in vanilla spnpccharacters.xml).

   Build green, 2,323/2,325 tests pass. Validation tool reports 0 deprecated
   flag hits remaining.

   Research: docs/migration/v1.4.x-equipment-overhaul.md +
   docs/migration/templates/equipment-rosters.md
   ```

## S5b — Author missing equipment rosters

### The mandatory roster matrix

Per v1.4.3 spec — each of TAOM's 12 custom cultures needs at minimum these 8 roster combinations (12 if the culture has kingdom-tier rulers):

| # | Flags | equipmentType | Purpose |
|---|---|---|---|
| 1 | `IsLordTemplate` | Battle (implicit, omit) | Male lord battle |
| 2 | `IsLordTemplate` | Civilian | Male lord civilian |
| 3 | `IsLordTemplate IsFemaleTemplate` | Battle (omit) | Female lord battle |
| 4 | `IsLordTemplate IsFemaleTemplate` | Civilian | Female lord civilian |
| 5 | `IsLordTemplate IsChildEquipmentTemplate` | Civilian | Male lord child |
| 6 | `IsLordTemplate IsChildEquipmentTemplate IsFemaleTemplate` | Civilian | Female lord child |
| 7 | `IsLordTemplate IsTeenagerEquipmentTemplate` | Civilian | Male lord teen |
| 8 | `IsLordTemplate IsTeenagerEquipmentTemplate IsFemaleTemplate` | Civilian | Female lord teen |
| 9 | `IsKingdomRulerTemplate` | Battle (omit) | Male king battle (optional, kingdom-tier only) |
| 10 | `IsKingdomRulerTemplate` | Civilian | Male king civilian (optional) |
| 11 | `IsKingdomRulerTemplate IsFemaleTemplate` | Battle (omit) | Female queen battle (optional) |
| 12 | `IsKingdomRulerTemplate IsFemaleTemplate` | Civilian | Female queen civilian (optional) |

12 cultures × 8 mandatory = 96 minimum rosters. Kingdom-tier cultures (those that own kingdoms in `taom_spkingdoms.xml`: gondor, mordor, erebor, rivendell, mirkwood, isengard, gundabad, dolguldur, lothlorien, umbar, possibly shaghana/abanissa) may also need the kingdom-ruler set (+4 more = 12 × 4 = up to 48 additional).

### TAOM custom cultures (from `Main/_Module/ModuleData/taom_spcultures.xml`)

Per the audit script: **12 cultures**: erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, umbar, dolguldur, gondor, mordor, shaghana, abanissa.

The 6 XSLT-renamed vanilla cultures (vlandia=Rohan, empire=Dunland, etc.) inherit vanilla rosters and don't need TAOM-authored rosters.

### Procedure

1. **Run the audit script** to get the per-culture gap matrix:
   ```powershell
   python tools/audit_equipment_roster_coverage.py
   ```
   Output: `docs/migration/equipment-roster-coverage.csv`. Each row = `culture, required_combo, equipmentType, found_count, status`.

2. **Per culture, identify which combos exist** (some may have rosters under existing IDs that just need flag tagging). The combos that are TRULY missing need new rosters authored.

3. **Author each missing roster** using vanilla 1.4.5 as the structural reference. Read `templates/equipment-rosters.md` for the canonical shape. Each roster:
   - Goes in `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_<culture>.xml`
   - Has `culture="culture.<culture>"` attribute (required in 1.4.3+; warning if missing)
   - Has `<Flags ...>` child element (single element, boolean attributes per flag)
   - Has `<EquipmentSet ...>` children with `equipmentType="Civilian"` (or omit for Battle)
   - References real LOTRLOME_Armory item IDs (cross-reference with `tools/validate_gondor_refs.py` extended to all cultures — see step 5)

4. **Use LOTRLOME_Armory item IDs** per CLAUDE.md item-prefix map:
   - Gondor: `sk_gd_ano_*` (Anorien), `sk_gd_mns_*` (Minas Tirith), `sk_gd_osg_*` (Osgiliath), `sk_gd_cair_*` (Cair Andros), `sk_gd_ith_*` (Ithilien) + the 8 phase-2 families (Lossarnach/PG/Har/Anf/Sere/Leb/Bel/Lam)
   - Mordor / Isengard / Dol Guldur: use the corresponding LOTRLOME armory paths under `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<culture>\`
   - Pick representative noble armor + helmet + boots + gloves + weapon + horse for battle rosters; pick noble civilian clothing for civilian rosters

5. **Underwear-bug gate** — every authored roster's item IDs must resolve in `LOTRLOME_Armory`. Extend `tools/validate_gondor_refs.py` (currently Gondor-only) to scan all 12 cultures, or write a parallel tool. The validator should:
   - Parse every `<EquipmentSet>` in the per-culture file
   - Extract every `id="..."` from inside the set
   - Cross-check against `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<culture>\*.xml`
   - Fail loud on any unresolved ID

6. **Per-culture stylistic consistency** — Gondor noble != Mordor noble. Use the closest-vanilla analogue (Empire = Gondor structurally, Sturgia = Erebor/Dale) as a starting point, then substitute LOTR-themed items. The character/equipment template docs in `templates/` give you the shape; the LOTRLOME_Armory paths give you the items.

7. **Validation gate:**
   ```powershell
   python tools/audit_equipment_roster_coverage.py
   # Must report all 12 cultures passing the 8 mandatory combinations.
   # Kingdom-tier cultures should also pass the 4 kingdom-ruler combinations.
   
   # New: roster-reference validator (extend tools/validate_gondor_refs.py per step 5)
   python tools/validate_all_culture_refs.py
   # Must report 0 unresolved item IDs.
   ```

8. **Build + test gate:**
   ```powershell
   dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true
   dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true
   # Both must remain green.
   ```

9. **Commit S5b:**
   ```
   migration(s5b): author 12×8 mandatory equipment rosters per v1.4.3 contract

   v1.4.3 requires every culture to provide at minimum 8 roster combinations
   (IsLordTemplate × {male, female} × {battle, civilian} +
    IsLordTemplate IsChildEquipmentTemplate × {male, female} civilian +
    IsLordTemplate IsTeenagerEquipmentTemplate × {male, female} civilian).
   Kingdom-tier cultures also get IsKingdomRulerTemplate × {male, female} ×
   {battle, civilian}.

   Authored 96 (or N, replace) rosters across 12 TAOM custom cultures:
   erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, umbar,
   dolguldur, gondor, mordor, shaghana, abanissa.

   All item IDs cross-checked against LOTRLOME_Armory; underwear-bug gate
   green via tools/validate_all_culture_refs.py.

   Coverage audit: 12/12 cultures pass mandatory matrix, K/12 pass kingdom-tier
   matrix (where applicable).

   Build green, 2,323/2,325 tests pass.

   Research: docs/migration/templates/equipment-rosters.md mandatory matrix +
   vanilla SandBoxCore/sandboxcore_equipment_sets.xml as structural reference
   ```

## TRACKING.md updates (do both at session end)

After S5a + S5b both committed, update `docs/migration/TRACKING.md`:

- Mark **S5a** row as ✅ Complete with date, link to commit SHA.
- Mark **S5b** row as ✅ Complete with date, link to commit SHA.
- Update the "Open issues" section: remove the S5a + S5b entries.

## Done criteria

| Gate | Pass condition |
|---|---|
| Mechanical migration | `tools/migrate_equipment_type_1_4_3.py --dry-run` reports 0 files needing migration |
| Deprecated flags | `tools/validate_equipment_flags_1_4_3.py` returns exit 0 |
| Coverage matrix | `tools/audit_equipment_roster_coverage.py` reports all 12 cultures passing |
| Item resolution | New `tools/validate_all_culture_refs.py` (extend Gondor-only validator) reports 0 unresolved IDs |
| Build | `dotnet build Main/TAOM.csproj` 0 errors |
| Tests | `dotnet test TAOM.Tests/TAOM.Tests.csproj` 2,323/2,325 pass |
| Commits | 2 commits: `migration(s5a):` + `migration(s5b):` on `bannerlord-1.4.5` |
| Push | `git push origin bannerlord-1.4.5` |
| Docs | TRACKING.md S5a + S5b rows marked complete |

## Out of scope for this session

- C# code changes (S0–S5 already complete in parallel session — don't touch)
- Harmony patches, GameModels, adapters (compile-clean, untouched here)
- Smoke testing the game (S6 — separate session after both S5a/S5b + main C# session land)
- Feature validation (S7–S10 — later)
- Codex review of XML changes (S11 — later batch review)
- Authoring vanilla-aligned rosters for the 6 XSLT-renamed cultures (Rohan, Dunland, etc. — they inherit vanilla rosters, no TAOM-authored rosters needed)
- Adopting `Stealth` equipment type for the disguise system (post-migration enhancement)
- Restructuring duplicate battle+civilian equipment sets (TaleWorlds devs acknowledged this is a known pain point they aren't solving)

## If you get blocked

- **Tool runs but produces unexpected output:** read the tool's source (under `tools/`) and the spec at `docs/migration/v1.4.x-equipment-overhaul.md`. The tools were authored with dry-run verification but not `--apply` verified end-to-end.
- **Manual roster author looks wrong:** cross-reference vanilla equivalent in `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\sandbox_equipment_sets.xml` (search for `IsLordTemplate="true"` to find vanilla lord rosters).
- **Item ID doesn't resolve:** check the exact LOTRLOME path via `Get-ChildItem` — the `sk_gd_*` prefix system is documented in CLAUDE.md "Equipment & Armory" section.
- **Coverage audit still shows gaps after authoring:** the audit matches by `(culture, exact-flag-combination, equipmentType)`. Common gotcha: a roster might have `IsLordTemplate IsFemaleTemplate` but missing the `equipmentType="Civilian"` attribute — the audit treats it as Battle and looks for the Civilian variant separately.
- **Worktree conflicts when merging back:** rebase against the latest `bannerlord-1.4.5` HEAD (the C# session may have committed first). XML conflicts are easy — file-level merge usually works.

## Reference paths

| Path | What's there |
|---|---|
| `docs/migration/templates/` | All "what right looks like" reference docs |
| `docs/migration/v1.4.x-equipment-overhaul.md` | The migration spec |
| `docs/migration/equipment-roster-coverage.csv` | Per-culture audit output (regenerate via audit script) |
| `Main/_Module/ModuleData/equipmentsets/` | Where TAOM's per-culture rosters live |
| `Main/_Module/ModuleData/characters/` | NPC + lords XML (also has `civilian="true"` to migrate) |
| `Main/_Module/ModuleData/troops/` | Troop XML files (inline rosters here — usually NOT to be migrated) |
| `Main/_Module/ModuleData/lords.xslt` | XSLT passthrough (line 389 has `civilian="true"`) |
| `tools/` | All migration tools (Python + PowerShell) |
| `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\sandbox_equipment_sets.xml` | Vanilla 1.4.5 lord rosters (use as structural template) |
| `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\sandboxcore_equipment_sets.xml` | Vanilla 1.4.5 kingdom-ruler rosters (use as template for IsKingdomRulerTemplate combos) |
| `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<culture>\` | LOTR-themed item IDs for each culture |

You have everything you need. The C# session is on the same branch and may commit/push first — that's fine, just rebase if needed.
