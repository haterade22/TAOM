# Plan 136: Gate XML schemas, data references, banners, faction cards and generated blocks on the 1.4.x line

> **Executor instructions**: Follow this plan part by part, step by step. Run every verification
> command and confirm the expected result before moving on. If anything in "STOP conditions" occurs,
> stop and report; do not improvise. Parts A to E are each independently landable in the order given,
> and each has its own done criteria. Do not edit `plans/README.md`: report status, commit hashes and
> measured counts in your hand-off; the integrator records them after the merge.
>
> **Drift check (run first, in the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- tools/validate_moduledata.py tools/taom_schema.py tools/taom_query.py tools/taom_mcp_server.py tools/tests/test_validate_moduledata.py tools/tests/test_taom_query.py tools/test_hooks.sh tools/lint_docs.py tools/tests/test_lint_docs.py tools/audit_mbproj_registration.py tools/audit_mount_parity.py tools/apply_culture_skills_traits.py tools/apply_isengard_troop_revamp.py tools/translate_with_claude.py tools/generate_new_faction_kingdoms.py tools/insert_new_factions.py tools/promote_borrowed_cultures.py tools/check_prefab_budget.py tools/audit_scene_names.py .claude/hooks/check-moduledata-validation.sh .claude/hooks/check-doc-config-drift.sh .claude/rules/moduledata-validation.md Main/_Module/ModuleData/heroes.xslt Main/_Module/ModuleData/spclans.xslt Main/_Module/ModuleData/characters/clans.xml Main/_Module/ModuleData/factionmap/factions.json Main/_Module/ModuleData/taom_xslt_strings.xml docs/features/moduledata-validation.md Dependencies/Foundation/PatchShield.cs TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs TAOM.Tests/Infrastructure/Dependencies`
> Changes by earlier WPs of this programme are expected (WP103: hook output shape, `lint_docs.py --drift-only`;
> WP107: `PatchShield.cs`; WP108: `validate_moduledata.py`, `taom_schema.py`, the moduledata hook and rule;
> WP116: hook command reading; WP131: `PrefabExtensionBrushTests.cs`; WP133: `generate_new_faction_kingdoms.py`,
> banner keys; WP135: `translate_with_claude.py`, language files). Compare the live files with the anchors
> quoted below and STOP only on a real mismatch.

## Status

- **Priority**: P2
- **Effort**: L (five parts; each fits one executor session)
- **Risk**: MED (new ERROR codes reach the commit hook; a false positive blocks data commits. Every gate here starts clean on the data measured at planning, or says so)
- **Depends on**: WP108 (collision-body passes, `_run_main`, `CommitGateCoverageTests._named_codes`), WP117 (`tools/audit_battle_equipment_memory.py`), WP131 (`TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs`), WP133 (`upsert()` in place, `tools/tests/test_generate_new_faction_kingdoms.py`). Transitively WP101 to WP105 (WP104 `RepoPaths`), WP107 (`PatchShieldFinalizerTests.cs`).
- **Category**: data / tests / dx
- **Wave / lane**: wave 5, lane A
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (no feature module here), D8 (subjects carry `v2.0.28`), D9 (no CI; every gate runs locally), D10, D13 (a data fix only when verified on 1.4.x data and needing no OUT content), D15 (`taom_body_length` is never allowlisted), D19 (git only through the Bash tool), D20 (data-touching commits need the refreshed laptop Armory)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`, 2026-11-04. (`origin/bannerlord-1.5.x` has since moved to `ca1bc5f2e`; it changes `tools/data_gates.py`. Always read the pinned SHAs named here, never the moving ref.)
- **Issue**: port of #484, #621, #696, #729, #818, #949, #951, #974, #975, #976, #979, #980, #983, #984, #987, #371. New work (validator header, prefab budget, scene-name audit): orchestrator creates the issue before Part B lands; cite it as `Refs #<n>`. The executor never creates one: with no number in hand at B8, D1 or D2, omit that `Refs` trailer and name the commit in the hand-off.

## Why this matters

The maintainer's history on 1.4.8 is many infinite loading screens, some caused by wrong XML data. The engine
validates XML against its own XSD only to print a line and load the file anyway (`MBObjectManager.ValidationEventHandler`), so
nothing in the 1.4.x commit path checks a file against the schema the engine reads it with. An undefined `Faction.X`
loads a settlement ownerless; an item in the wrong slot is dropped with a failed assert; a removed troop id leaves a hollow
character in a save. Four data defects are live today and fixed here: seven clan banners name icons or colours that do not exist,
Mordor's faction card promises +10% party size against a +20% feat, three `heroes.xslt` templates never match, and
the Isengard generator would re-write the skirt to the Cape slot on its next run. After this plan, one command
(`python tools/data_gates.py`) says PASS, FAIL or NOT RUN for what changed, and the commit hook blocks each new ERROR. These are
gates: the XML-DATA lens found no data defect that explains the player freezes, and this plan does not claim to cure one.

## Current state

### Files and roles (1.4.x at `1b00d1acd`; line numbers are that tree's, before WP108 and later)

| Path | Role |
|---|---|
| `tools/validate_moduledata.py` (230 lines) | CLI over the engine. `main()` `:135-226`; `issues = validator.run()` `:194`; `if game_modules.exists():` branch `:195-200` holds `generator_item_ref_issues` and the only sort; `sys.exit(main())` `:230`. WP108 adds `import re`, `missing_collision_body_issues`, `borrowed_body_issues`, `BODY_CODE` |
| `tools/taom_schema.py` | Engine. `Registries` `:66-84`; `RefKind` `:143`; `REF_KINDS` `:162-180`; `Validator.__init__` `:208`; `issues += self._armour_slot_coverage()` `:232`; `_NPC_BLOCK_RE` `:886`, `_INLINE_ROSTER_RE` `:941`, `_CIVILIAN_ATTR_RE` `:946`, `_SLOT_ANY_QUOTE_RE` `:949`; `_BODYPROP_DEF_RE` `:1649`; `_scan` `:1693`, `_scan_files` `:1709`; `build_registries` `:2149`, its items line `:2188`, no-install reset `:2231`, floors `:2241-2250`, return `:2252-2266` |
| `tools/tests/test_validate_moduledata.py` | Validator tests. `CommitGateCoverageTests` `:2071` reads the hook's `--code` list |
| `.claude/hooks/check-moduledata-validation.sh` | Commit gate; `--code` allowlist; only rc 1 blocks |
| `tools/_gamedir.py` | `game_dir(default)` `:32`, `game_modules(default)` `:45` |
| `tools/generate_new_faction_kingdoms.py` | After WP133 its `upsert()` replaces in place and carries the checks `_marker_region` will own |
| `tools/insert_new_factions.py` | `upsert_before` `:151`; identical to 1.5.x `c73cf2961^` |
| `tools/promote_borrowed_cultures.py` | `upsert` `:300`, strip-and-append; differs from 1.5.x by about 390 lines |
| `tools/audit_mbproj_registration.py` | Regex audit; unchanged since the fork |
| `tools/lint_docs.py` | `_changed_markdown_lines(base)` `:1006`, `check_ai_dashes` `:1046`, `_INLINE_CODE_RE` `:947`, `_LINK_TARGET_RE` `:948`, `iter_lines_outside_code_fences` `:290`; WP103 adds `--drift-only` |
| `tools/check_prefab_budget.py` | `DEFAULT_DIR` hardcodes `E:\...\TAOM_Map\Prefabs` `:22-25`; counts one folder |
| `tools/audit_scene_names.py` | `SETTLEMENT_FILES` `:25-30` includes `TAOM_shadow`, a file the engine never loads |
| `Main/_Module/ModuleData/factionmap/factions.json:1045` | `{=taom_faction_dominion_of_mordor_bonus_1}+10% party size limit (Sauron's Hordes)` |
| `Main/Features/CulturalFeats/TaomCulturalFeats.cs:912-915` | `_mordorPartySize.Initialize(... 0.2f, isPositiveEffect: true ...)` |
| `Main/_Module/ModuleData/heroes.xslt:612, 817, 903` | The three dead templates `Hero[@id='lord_1_9_5']`, `lord_1_52_4`, `lord_1_71_1` |

### Measured at planning, read-only (working tree `c5b84fb41`, laptop Armory v2.0.26, TAOM_Map v2.0.27)

- 1.5.x tip `validate_xml_schemas.py` on this tree: repo `PASS: 93 file(s) validated, 0 failed, 0 registration(s) resolve to nothing`. `--live`: `FAIL: 214 file(s) validated, 1 failed`: `LOTRAOM_horses.xml` L441, L470, L510 `family_type` on `<Horse>` (inert; `HorseComponent.Deserialize` never reads it), plus `NO SCHEMA` for `ModuleSounds`.
- Its test file (`e45f6df2c`): 121 tests, 1 failure, `LiveTaomMapRegistrationTests` (`EXPECTED_IDS = ["Settlements"]`; the laptop TAOM_Map registers 8: `Factions`, `Items`, `Kingdoms`, `NPCCharacters`, `SPCultures`, `Settlements`, `WorkshopTypes`, `partyTemplates`). Emptying the allowlist also fails `TaomExtensionAttributeAllowlistTests` (handled in B1).
- XSLT coverage (lxml simulation of `XsltTemplateCoverageTests`): 1,050 templates over 8 stylesheets, exactly 3 dead, the three above.
- `taom_spcultures.xml` on `1b00d1acd`: 64 comments, 0 inside a child list.
- Custom Battle picker on `1b00d1acd`: 22 rows, 10 sieges, 0 failures on this laptop's TAOM_Map.
- Faction cards: only `dominion_of_mordor` mismatches (`10` quoted, feat `0.2`). The other 11 rows match.
- Banner gate (`487e3d9a9`'s test): 7 keys fail: `clans.xml:410, 593, 688, 703, 736` (icons), `clans.xml:688, 703`, `spclans.xslt:780, 793` (colours). Those are exactly the seven the commit fixes.
- Faction refs: 235 clans (145 repo, 95 vanilla, minus 5 stripped bandit clans), 1,442 `Faction.` refs over repo, Armory and TAOM_Map, 0 unresolved.
- Item registry with the `<Items>`-root rule: dropped 0 ids over the scanned roots. Caution: the 1.4.x tip's marketplace config blacklists `sk_mordor_mouth_of_sauron_helm` (`d906fc921`), defined by no `<Items>` document on this laptop per the classifier; it only ever sat in the registry through that config row.
- `v2.0.28` tag: 855 troop ids. `origin/bannerlord-1.4.5` and `origin/fix/public-armory-ids-145`: 857 (the tag lacks `ironpass_ram_herder`, `ironpass_ram_marshal`). So the baseline must come from the branch, not the tag.
- Townsfolk rule: 22 `characters/npcs_*.xml`, 0 civilian-only. Weapon "no common slot" rule: 385 multi-set troops, 0 offenders. The launcher half was not measured.
- `audit_mount_parity.py --strict` (`f1675c058`): spider, elephant, mumakil all `total`, rc 0.
- `apply_culture_skills_traits.py --check` (`f1675c058`): **rc 1**, `DRIFT ... only in the generator (1): taom_canonical_lord_I1_0_skills`. Real drift between generator and file. Report it; do not fix it here.
- `audit_mbproj_registration.py` (`bc30b76da` shape): 0 errors, 1 warning (`soln_voice_definitions` x4 on TAOM, known safe); its test file 36 tests, 0 failures.
- Each of the 13 `NATIVE_BY_TYPE` strings occurs in the installed `bin/Win64_Shipping_Client/TaleWorlds.Native.dll`. Their callers are UNVERIFIED on v1.4.8 (see D3).
- `lxml` 6.1.3 is installed on the laptop (Python 3.14).

### Engine facts verified on the installed v1.4.8 (`pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`)

- `TaleWorlds.ObjectSystem.MBObjectManager`: `public static XmlDocument GetMergedXmlForManaged(string id, bool skipValidation, bool ignoreGameTypeInclusionCheck = true, string gameType = "")` `:877` (schema `ModuleHelper.GetXsdPathForModules` `:888`, `GetXmlPath` `:893`, `HandleXsltList` `:897, :908, :914`); `public static XmlDocument GetMergedXmlForNative(string id, out List<string> usedPaths)` `:920`; `private static bool HandleXsltList(string xslPath, ref List<string> xsltList)` `:949`; `public static XmlDocument CreateMergedXmlFile(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation)` `:966`; `private static void LoadXmlWithValidation(string xmlPath, string xsdPath, XmlDocument xmlDocument)` `:1061`; `InjectOptionalAttrToAllComplexTypes(xmlSchemaSet, "boolean", "_replaceWhileMerging")` `:1092`; `private static void ValidationEventHandler(object sender, ValidationEventArgs e)` `:1324`.
- `TaleWorlds.ObjectSystem.XmlResource`: `public static List<MbObjectXmlInformation> MbprojXmls` `:30`; `public static void GetMbprojxmls(string moduleName)` `:114` (`new StreamReader(mbprojPath)` `:121`); `public static void GetXmlListAndApply(string moduleName)` `:149` (`:153`).
- `TaleWorlds.ModuleManager.ModuleHelper`: `GetXmlPathForNative` `:211`, `GetXmlPath` `:232`, `GetXsltPath` `:237`, `GetXsdPathForModules` `:242`, `GetXsdPath` `:247`.
- `TaleWorlds.Core.Equipment`: `public void DeserializeNode(MBObjectManager objectManager, XmlNode node)` `:204`; assigns only when `IsItemFitsToSlot` passes `:214`, else `Debug.FailedAssert` `:220`; `public static bool IsItemFitsToSlot(EquipmentIndex slotIndex, ItemObject item)` `:445`.
- `TaleWorlds.Core.BasicCharacterObject`: `public virtual Equipment FirstBattleEquipment => AllEquipments.FirstOrDefaultQ((Equipment e) => e.IsBattle);` `:108` (null for civilian-only).
- `TaleWorlds.CampaignSystem.CultureObject:419-424`: `notable_templates` walks `item5.ChildNodes` and calls `objectManager.ReadObjectReferenceFromXml<CharacterObject>("name", childNode7)` per node.
- `TaleWorlds.MountAndBlade.BannerlordMissions`: `Level1Tag = "level_1"` `:26`, `Level2Tag` `:28`, `Level3Tag` `:30`, `SiegeTag = "siege"` `:32`; `public static Mission OpenSiegeMissionWithDeployment(string scene, ... int sceneUpgradeLevel = 0, ...)` `:160`, `sceneLevels` switch `:162`.
- `TaleWorlds.MountAndBlade.CustomBattle.CustomGame`: `LoadCustomBattleScenes(mergedXmlForManaged)` `:117`, `private void LoadCustomBattleScenes(XmlDocument doc)` `:165`, reads `is_siege_map` `:240`.
- Managed mbproj requesters in the v1.4.8 dump: `GetMergedXmlForNative("soln_skins" | "soln_item_holsters" | "soln_action_sets" | "soln_action_types" | "soln_animations" | "soln_voice_definitions" | "soln_sound_event_data" | "soln_sound_parameter_data")`, the `"soln_" + ...` callback, and `"soln_soundtrack"`. `worldmap_color_grades` appears **nowhere** in `C:\Decompiled_Bannerlord\_categories_v1.4.8`, so `soln_worldmap_color_grades` is dropped on this line.

### What earlier WPs must have provided (A0 checks them; STOP if absent)

WP104 `TAOM.Tests/Infrastructure/RepoPaths.cs` (`RepoPath`, `CSharpFiles`, `StripComments`, `CodeRoots = { "Main" }`). WP103 `--drift-only` in `lint_docs.py`, `hookSpecificOutput` in the hooks, `decision_of()` in `tools/test_hooks.sh`. WP107 `TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs`. WP108 `def missing_collision_body_issues`, `_run_main`, `def _named_codes` and `_unreached_passes`, `tools/audit_armory_refs.py`. WP117 `def slot_rejections` in `tools/audit_battle_equipment_memory.py`. WP131 `PrefabExtensionBrushTests.cs` with `DeclaredBrushes()`. WP133 `closing tag` refusal in `generate_new_faction_kingdoms.py`'s `upsert`.

### Conventions that bind this work

- **TDD**: failing test first (RED, GREEN, REFACTOR). A gate that starts clean proves RED with a throwaway break, reverted before commit.
- **ADR-007** adapters (services never touch sealed TaleWorlds types), **ADR-002** thin entry points under 150 lines, **ADR-003** no `#region`, **ADR-004** no `[Obsolete]`, **ADR-005** no `#if DEBUG` outside IoC registration. No runtime C# changes in this WP except one comment (E2).
- **`CommitGateCoverageTests`**: an ERROR code the validator emits must be named on the hook's `--code` list in the same commit, or the suite fails. Every `*_issues` pass must be called from `main()`.
- **Environment failures**: a missing `lxml`, install or Armory is reported, not fixed.
- **MCM json2 rule** and `.claude/rules/harmony-patches.md` are not engaged: no setting and no patch here.
- **Localization**: this WP edits translated values of one existing key. It adds no key. Never run `tools/translate_with_claude.py` (it costs money and belongs to the maintainer).

## Commands you will need

Run from the worktree root `C:\Users\mikew\source\repos\taom-port\136`. `$WT` means that folder; `$NOTES` means `C:/Users/mikew/source/repos/taom-port/136-notes` (outside the worktree; create it).

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~<Class>` | 0 failed |
| One tool test | `python -m unittest discover -s tools/tests -t . -p "<file>.py" 2>&1 \| tail -4` | `OK` |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure vs your A0 baseline |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline in `plans/README.md` |
| Hooks | `bash tools/test_hooks.sh` | no new failure vs your A0 baseline |
| Docs | `python tools/lint_docs.py` | no new finding in touched files |
| Dash check | `git diff --cached -U0 \| python -X utf8 -c "import sys; b=[l for l in sys.stdin.read().splitlines() if l.startswith('+') and ('\u2014' in l or '\u2013' in l)]; print(len(b)); print('\n'.join(b))"` | `0` for prose you wrote; a dash inside a file taken verbatim from a pinned 1.5.x blob (a test's own comment) is not prose you wrote, but remove it from anything you reword |

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj:133-136`). Never `./build.ps1`. Never two
builds at once. Environment: the maintainer's laptop; Bannerlord v1.4.8 (build 119303) at
`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (env `BANNERLORD_GAME_DIR`). Never write
into the game install. Set `MSYS_NO_PATHCONV=1` before any `git show <ref>:.claude/...`. The worktree has CRLF working
copies of LF blobs (`core.autocrlf`), so `git apply` of a raw 1.5.x diff fails: edit with the helper below or the Edit tool.

**Edit helper**: save as `$NOTES/wp136_edit.py`. Scripts in this plan `import` it (`sys.path.insert(0, "<NOTES>")`).

```python
"""WP136 helpers: read pinned blobs, edit by exact anchors, keep each file's line endings."""
import subprocess, sys
def show(spec):
    return subprocess.run(["git", "show", spec], capture_output=True, check=True).stdout.decode("utf-8").replace("\r\n", "\n")
def load(path):
    raw = open(path, "rb").read().decode("utf-8")
    return raw.replace("\r\n", "\n"), ("\r\n" if "\r\n" in raw else "\n")
def save(path, text, nl):
    open(path, "wb").write(text.replace("\n", nl).encode("utf-8"))
def once(text, old, new):
    if text.count(old) != 1:
        sys.exit(f"STOP: anchor found {text.count(old)} times: {old[:80]!r}")
    return text.replace(old, new)
def cut(text, start, end=None):
    i = text.index(start)
    return text[i:] if end is None else text[i:text.index(end, i + 1)]
```

## Scope

**In scope** (the only files you may modify or create):
- Validator and engine: `tools/validate_moduledata.py`, `tools/taom_schema.py`, `tools/taom_query.py`, `tools/taom_mcp_server.py`, `tools/schemas/*.json` only if B5 says so, `tools/shipped_troop_ids.txt` (new), `tools/validate_xml_schemas.py` (new), `tools/data_gates.py` (new), `tools/_marker_region.py` (new)
- Tools: `tools/audit_mbproj_registration.py`, `tools/audit_mount_parity.py`, `tools/apply_culture_skills_traits.py`, `tools/add_townsfolk_battle_rosters.py`, `tools/apply_isengard_troop_revamp.py`, `tools/translate_with_claude.py` (the `--module` line), `tools/analyze_kingdom_armour.py`, `tools/fix_upgrade_armour_regressions.py` (comments), `tools/generate_new_faction_kingdoms.py`, `tools/insert_new_factions.py`, `tools/promote_borrowed_cultures.py`, `tools/check_prefab_budget.py`, `tools/audit_scene_names.py`, `tools/lint_docs.py`, `tools/check_external_loc_coverage.py` (one import, only if WP135's local copy is there), `tools/test_hooks.sh`
- Tool tests under `tools/tests/`: `test_validate_moduledata.py`, `test_taom_query.py`, `test_lint_docs.py`, and new `test_validate_xml_schemas.py`, `test_banner_icon_ids.py`, `test_data_gates.py`, `test_marker_region.py`, `test_region_writers_in_place.py`, `test_audit_mbproj_registration.py`, `test_apply_culture_skills_traits_check.py`, `test_audit_mount_parity_strict.py`, `test_translate_module_required.py`, `test_check_prefab_budget.py`, `test_audit_scene_names.py`
- Hooks and rule (the lines documenting the exact mechanism only): `.claude/hooks/check-moduledata-validation.sh`, `.claude/hooks/check-doc-config-drift.sh`, `.claude/rules/moduledata-validation.md`
- Data, only the rows named: `Main/_Module/ModuleData/heroes.xslt`, `taom_xslt_strings.xml`, `characters/clans.xml`, `spclans.xslt`, `factionmap/factions.json`, `taom_module_strings.xml`, the 12 `Languages/<L>/std_taom_xslt_strings_*.xml` and 12 `Languages/<L>/std_taom_module_strings_*.xml` (named rows), the 12 `tools/translation_cache/<l>.json` (one key)
- C# tests: `TAOM.Tests/Core/CultureXmlCommentPlacementTests.cs`, `TAOM.Tests/Migration/XsltTemplateCoverageTests.cs`, `TAOM.Tests/Features/CustomBattles/CustomBattleSceneLiveDataTests.cs`, `TAOM.Tests/Features/FactionMap/FactionMapDataTests.cs`, `TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs`, `TAOM.Tests/Infrastructure/{EntryPointLineCountGateTests.cs,EntryPointLineCountBaseline.txt,ServiceSignatureGateTests.cs,ServiceSignatureBaseline.txt,ServiceLocatorGateTests.cs,PrefabSpriteTests.cs,PrefabExtensionBrushTests.cs}`, `TAOM.Tests/Infrastructure/Dependencies/{BundledDependencyManifestTests.cs,PatchShieldFinalizerTests.cs}`
- `Dependencies/Foundation/PatchShield.cs` (one comment), `.gitattributes` (one line), `docs/reference/lotrlome-armory-snapshot/project.mbproj`
- Docs: `docs/features/moduledata-validation.md`, `docs/features/doc-health-linter.md`, `docs/modding/banners-and-heraldry.md`, `docs/modding/file-catalogue.md`, `docs/modding/validation-and-testing.md`
- Maintainer only, Step E3 (the executor never edits these): `docs/adrs/002-thin-entry-points.md`, `docs/adrs/007-adapter-pattern.md`

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` are out of scope; nothing here needs them. If one seems to, STOP.

**Out of scope** (do NOT touch): `CHANGELOG.md` (D1), `CLAUDE.md`, `AGENTS.md`, `.ai/**`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md`, `docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `docs/adrs/*.md` (the maintainer alone applies Step E3), `.claude/settings*.json`, `.claude/skills/**`, `.claude/agents/**` (WPs 138, 142, 143 own them; hand-offs are under Maintenance notes). `plans/README.md`. Any feat value, garrison number, troop, roster, kingdom or culture. `taom_spcultures.xml` and `spcultures.xslt` (`executioner` is a v1.5.x field). `TaomPartyWageModel`, `WageModifierService`, `module_strings.xslt` and the `str_adjective_for_faction` strings (bc5b5d71d's v1.5.2 half; v1.4.8 still reads them). `tools/generate_career_kits.py`, `tools/audit_fbx_lods.py`, `tools/generate_enlistment_rosters.py`, `tools/generate_arthedain_troops.py`, `tools/tests/test_generator_regions_in_sync.py`. `tools/audit_armory_refs.py` beyond what WP108 left (never `12afef4d2` or `f1675c058`'s companion checks). Any `tools/*.py --apply`.

## Git workflow

1. Worktree (Bash tool, from the main checkout):
   `git -C /c/Users/mikew/source/repos/TAOM worktree add /c/Users/mikew/source/repos/taom-port/136 -b port148/136-data-integrity-gates bannerlord-1.4.5`. Work only there.
2. Run git only through the Bash tool (D19). Never `git add -A`, `-u` or `.`; stage explicit paths.
3. Cherry-pick only with `git cherry-pick -n <sha>`; never let cherry-pick, `--continue`, `-C`, `-c` or `--amend --no-edit` create the commit.
4. Bookkeeping from a cherry-pick: a conflicted path gets `git checkout --ours <path>` then `git add <path>`; a cleanly changed one gets `git checkout HEAD <path>`. A path absent on this line that the pick conflicts on: `git rm --cached -q <path>; rm -f <path>`.
5. Commit: `git add <paths>`, write the message to `$NOTES/msg.txt`, then in the NEXT separate Bash call `git commit -F $NOTES/msg.txt`. Never in the same call as `git add`, never in a parallel call, never through PowerShell. Never `--no-verify`; if a hook blocks, read its message and fix the cause or STOP.
6. Message template (subject at most 72 characters; keep the 1.5.x type and scope where one exists):
   ```
   <type>(<scope>): v2.0.28 - <description>

   <Body for 1.4.x readers, wrapped at 72: what changed on the 1.4.8 line
   and why. It becomes the release note (D1). No em or en dashes. No
   Co-Authored-By or "Generated with" line.>

   Research: <what was verified on v1.4.8>
   Not-tested: <what needs an in-game check>
   Ported-from: <40-hex sha>      (one line per included 1.5.x commit; omit for new work)
   ```
   Full SHAs for the trailers: `git rev-parse <short>`. A partial port still lists the SHA; the disposition table says what was left.
7. Build and filtered tests after each C# commit; tool tests after each Python commit; the full suite at the end of each part. Never push, open a PR, tag or merge.

---

## Part A: stand-alone data gates and the defects they find (C# tests, data)

Dependency: A2, A4 and A5 stage `Main/_Module/ModuleData` files, so they need A0's Armory check.

### Step A0: baseline and prerequisites

```bash
cd /c/Users/mikew/source/repos/taom-port/136 && mkdir -p ../136-notes
dotnet restore TAOM.sln
dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=
python -m unittest discover -s tools/tests -t . 2>&1 | tail -4
bash tools/test_hooks.sh 2>&1 | tail -3
python tools/validate_moduledata.py 2>&1 | tail -3
git grep -c -e 'def missing_collision_body_issues' -e 'def _run_main' -e '_unreached_passes' -- tools/validate_moduledata.py tools/tests/test_validate_moduledata.py
ls TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs tools/audit_battle_equipment_memory.py tools/audit_armory_refs.py tools/tests/test_generate_new_faction_kingdoms.py
git grep -n 'def slot_rejections' -- tools/audit_battle_equipment_memory.py
git grep -n 'closing tag' -- tools/generate_new_faction_kingdoms.py
git grep -c -e 'drift-only' -- tools/lint_docs.py
git grep -c -e 'hookSpecificOutput' -- .claude/hooks/check-moduledata-validation.sh
git grep -c -e '^decision_of()' -- tools/test_hooks.sh
grep -o '<Version value="[^"]*"' "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/SubModule.xml"
git grep -n 'def main(argv' -- tools/translate_with_claude.py
git grep -n '_parse_like_engine' -- tools/check_external_loc_coverage.py
ls tools/*.py | sed 's#tools/##' > ../136-notes/tools-at-start.txt
```

**Verify**: build exit 0; 0 test failures (record totals, tool-test and hook baselines, and the validator's per-code error count); every `ls` path listed and every grep before the Armory line non-zero, else STOP naming the missing WP (the `def main(argv` and `_parse_like_engine` greps may legitimately be empty). If the Armory version is `v2.0.26`: STOP before A2, A4 and A5 ("BLOCKED on WP101 Part A, D20"); A1, A3 and Parts B to E may run, but their data verdicts then describe v2.0.26 only: say so in the hand-off. Record the last two grep results for B6 and B1.

### Step A1: culture XML comment gate (`d28d9a7d3`, test only)

Why not a pick: its other files (`executioner` in `taom_spcultures.xml`, `spcultures.xslt`, `CultureExecutionerTests`) serve a v1.5.x field; v1.4.8 `CultureObject` has none and its `SPCultures.xsd` does not declare it (classifier, K-verified). `NotableTemplateGenderTests` is not ported: the gate it adds guards a v1.5.0 Trader start.

1. `git show d28d9a7d3:TAOM.Tests/Core/CultureXmlCommentPlacementTests.cs > TAOM.Tests/Core/CultureXmlCommentPlacementTests.cs`
2. Edit it: in the first `<para>` add the citation "(v1.4.8: `CultureObject.cs:419-424`)" after `MBObjectManager.ReadObjectReferenceFromXml("name", node)`. Replace the sentence "Shipped in v2.1.0 and fixed in v2.1.1 on the archived v1.5.0 line." with "Seen on an archived v1.5.0 build; the same walk exists on v1.4.8." Delete the fourth `<para>` (it cites the unported gender gate) and the trailing three-line comment after the test method.
3. RED by throwaway: in `Main/_Module/ModuleData/taom_spcultures.xml` insert `<!-- x -->` as the first child of the first `<notable_templates>`, then Build and Filtered `CultureXmlCommentPlacementTests`: 1 failed, naming that line. Then `git checkout Main/_Module/ModuleData/taom_spcultures.xml`.
4. Build, Filtered again: 0 failed.

Commit A1: `test(cultures): v2.0.28 - no XML comment inside a culture child list`. Stage the test. Trailers: `Research: v1.4.8 CultureObject.cs:419-424, MBObjectManager.LoadXmlWithValidation`; `Not-tested: none`; `Ported-from:` `d28d9a7d3`.

### Step A2: XSLT template coverage gate and three dead templates (`bc5b5d71d`, partial)

Simulation: the commit conflicts only in `CHANGELOG.md`, but most of it is v1.5.2 work, so take by hand.

1. `git show bc5b5d71d:TAOM.Tests/Migration/XsltTemplateCoverageTests.cs > TAOM.Tests/Migration/XsltTemplateCoverageTests.cs`. In its summary replace the two sentences "Bannerlord v1.5.2 stopped reading ... matching nothing." with "When an engine update stops reading a vanilla row, the templates over it go dead the same way." Keep the `heroes.xslt` sentence.
2. RED: Build, then `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~XsltTemplateCoverageTests`: 1 failed, listing exactly `heroes.xslt` `lord_1_9_5`, `lord_1_52_4`, `lord_1_71_1`. A fourth entry: STOP and report it (an earlier WP may have added a template or the vanilla data moved); do not delete it.
3. GREEN: run from the worktree root:
   ```python
   import glob, re, sys
   sys.path.insert(0, "C:/Users/mikew/source/repos/taom-port/136-notes")
   from wp136_edit import load, save
   IDS = {"lord_1_9_5": "TAOM_hero_1_9_5", "lord_1_52_4": "TAOM_hero_1_52_4", "lord_1_71_1": "TAOM_hero_1_71_1"}
   p = "Main/_Module/ModuleData/heroes.xslt"; t, nl = load(p)
   for hero in IDS:
       t, n = re.subn(r"\t<xsl:template match=\"Hero\[@id='%s'\]\">.*?</xsl:template>\n\n" % hero, "", t, flags=re.S)
       assert n == 1, hero
   save(p, t, nl)
   for p in ["Main/_Module/ModuleData/taom_xslt_strings.xml"] + glob.glob("Main/_Module/ModuleData/Languages/*/std_taom_xslt_strings_*.xml"):
       b = open(p, "rb").read()
       for key in IDS.values():
           b, n = re.subn(rb'[ \t]*<string id="%s"[^\n]*\n' % key.encode(), b"", b)
           assert n == 1, (p, key)
       open(p, "wb").write(b)
   print("ok")
   ```
   Do not touch `tools/translation_cache/*` (`bc5b5d71d` left those rows; they are inert) or `_taom_loc.pkl`.
4. Build; Filtered `"FullyQualifiedName~XsltTemplateCoverageTests|FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~LanguageDataXmlTests|FullyQualifiedName~UnregisteredLocalizationKeyBaselineTests"`: 0 failed. `git diff --numstat -- Main/_Module/ModuleData/Languages | wc -l` prints `12`.

Commit A2: `fix(xslt): v2.0.28 - drop three hero templates that never matched`. Stage the test, `heroes.xslt`, `taom_xslt_strings.xml`, the 12 files. Body: a stylesheet runs over the earlier modules' merged file, so a template naming a hero TAOM itself defines never fires (`MBObjectManager.CreateMergedXmlFile`, v1.4.8 `:966`); the texts never reached a player. Trailers: `Research: CreateMergedXmlFile v1.4.8; lxml simulation 1,050 templates, 3 dead`; `Not-tested: none`; `Ported-from:` `bc5b5d71d`.

### Step A3: Custom Battle scene gate (`d96167efa`, `babcb8335`, test end state)

Simulation: `d96167efa` clean, `babcb8335` conflicts because it edits the first's file. Take the `babcb8335` blob; leave the Edoras row (its scene is only in the unversioned TAOM_Map) and `generate_enlistment_rosters.py` (WP141 owns its test note).

1. `mkdir -p TAOM.Tests/Features/CustomBattles && git show babcb8335:TAOM.Tests/Features/CustomBattles/CustomBattleSceneLiveDataTests.cs > TAOM.Tests/Features/CustomBattles/CustomBattleSceneLiveDataTests.cs`
2. Edit: summary, delete "(the Edoras row of 2026-09-29 names a scene created that day); the #603 check was a one-off" (the words wrap over two `///` lines); replace the `SiegeLevels` comment's "(v1.5.3)" with "(v1.4.8 `BannerlordMissions.cs:26-32, :162`)"; in the `DeclaredLevels` summary delete the `rca-keyforce-art-wiring-2026-09-29.md` citation (absent on this line).
3. RED by throwaway: run the two fragment tests after temporarily changing `reader.Depth != 1` to `reader.Depth < 0`: `DeclaredLevels_CommentedOutReorderedAndEntityLevels_...` or `..._NoSceneLevelsBlock_...` fails. Restore. (The live test is green on this data, measured.)
4. `docs/modding/file-catalogue.md:35`: in the `custom_battle_scenes.xml` row replace the "not determined from the engine: ..." cell with: "`CustomGame` merges every module's `CustomBattleScenes` file (v1.4.8 `CustomGame.cs:117`) and `LoadCustomBattleScenes` (`:165`) reads each `Scene` row; `is_siege_map` at `:240`. Gate: `CustomBattleSceneLiveDataTests`." Keep the row's other cells.
5. Build, Filtered `CustomBattleSceneLiveDataTests`: 0 failed (3 tests; the live one is Inconclusive only without `TAOM_Map`).

Commit A3: `test(custom-battles): v2.0.28 - picker scenes exist, sieges name levels`. Trailers: `Research: v1.4.8 BannerlordMissions.OpenSiegeMissionWithDeployment, CustomGame.LoadCustomBattleScenes`; `Not-tested: none`; `Ported-from:` both SHAs.

### Step A4: faction card party-size gate and Mordor's card (`e166d8c84`, gate only)

Simulation: conflicts in three docs only, but it is a balance commit: **every feat value stays**.

1. RED. `TaomCulturalFeatsDefinitionTests.cs` (`LocateSource` is private at `:361`, in the same class): after `LocateSource`'s method add, verbatim from `git show e166d8c84:TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs` lines 433 to 456: the `ProductionEffectBonus` summary and method, `NormalizeWhitespace`, `ReadInitializeArgs`. If `NormalizeWhitespace` or `ReadInitializeArgs` already exists in the live file, keep the live one.
2. `FactionMapDataTests.cs`: before `private static IEnumerable<string> EnumerateStrings(JToken node)` (`:169`) insert the 12-row `FactionCard_PartySizePercent_MatchesProductionFeat` block and `PartySizePercentRegex` from `git show e166d8c84 -- TAOM.Tests/Features/FactionMap/FactionMapDataTests.cs` (its added lines). In its summary replace "until #818 cut the evil feats and left the cards promising +40 / +30 / +20 %" with "until Mordor's card kept +10% after its feat moved to +20%". Build; Filtered `FactionCard_PartySizePercent`: exactly 1 of 12 rows fails, `'dominion_of_mordor' card says +10% party size but _mordorPartySize is +20.0%` (or the same numbers in decimal form).
3. GREEN: fix the card text only, in the JSON, the English row, 12 language rows and 12 cache rows (a stale cache row would bring +10% back on the next translator run):
   ```python
   import glob, json
   KEY = "taom_faction_dominion_of_mordor_bonus_1"
   def fix(path):
       b = open(path, "rb").read()
       lines = b.split(b"\n")
       hits = [i for i, l in enumerate(lines) if (KEY + '"').encode() in l or (KEY + "}").encode() in l]
       assert len(hits) == 1, (path, hits)
       old = lines[hits[0]]
       new = old.replace(b"+10%", b"+20%").replace(b"+%10", b"+%20")
       assert new != old, path
       lines[hits[0]] = new
       open(path, "wb").write(b"\n".join(lines))
   for p in (["Main/_Module/ModuleData/factionmap/factions.json", "Main/_Module/ModuleData/taom_module_strings.xml"]
             + glob.glob("Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml")
             + glob.glob("tools/translation_cache/*.json")):
       fix(p)
   for p in glob.glob("tools/translation_cache/*.json"):
       json.load(open(p, encoding="utf-8"))
   print("ok")
   ```
   **Verify**: prints `ok`; `git diff --numstat | awk '$1=="1" && $2=="1"' | wc -l` prints `26` plus any test line you changed by one line (normally `26`); `git grep -n 'dominion_of_mordor_bonus_1[^0-9].*10%' -- Main tools/translation_cache` prints nothing.
4. Build; Filtered `"FullyQualifiedName~FactionMapDataTests|FullyQualifiedName~TaomCulturalFeatsDefinitionTests|FullyQualifiedName~LanguageFileCoverageTests"`: 0 failed.

Commit A4: `fix(faction-map): v2.0.28 - Mordor's card states its +20% party size`. Body: text only; the feat (`TaomCulturalFeats.cs:912-915`) has been +20% since `4f72e160e`; a test now ties every card's party-size number to its feat. Trailers: `Not-tested: the culture card and faction screen in game`; `Ported-from:` `e166d8c84`. (`6553b8c70`'s lesson: hand-off to WP142.)

### Step A5: banner key gate and seven banners (`487e3d9a9`)

Simulation: conflicts only in `docs/features/dale-lord-expansion.md` and `docs/features/lord-banners.md`.

1. RED: `git show 487e3d9a9:tools/tests/test_banner_icon_ids.py > tools/tests/test_banner_icon_ids.py`; one-tool-test: `test_every_shipped_banner_icon_exists` and `test_every_shipped_banner_colour_exists` fail, naming the seven rows in Measured. Any other row: finish this step, then report it in the hand-off and STOP before committing. Then `rm tools/tests/test_banner_icon_ids.py` (the pick recreates it and refuses to overwrite an untracked file).
2. `git cherry-pick -n 487e3d9a9`. `docs/features/lord-banners.md`: if it exists on the branch, `git checkout --ours` it and `git add`, else `git rm --cached -q` and `rm -f`. Same for `dale-lord-expansion.md`. A conflict in `clans.xml` or `spclans.xslt` (WP133's banner-colour work): keep ours and set only the seven `banner_key` values from `git show 487e3d9a9:<path>`, row by row.
3. One-tool-test: `OK`. `git status --short` lists only `characters/clans.xml`, `spclans.xslt`, `docs/modding/banners-and-heraldry.md`, the test.
4. Build; Filtered `"FullyQualifiedName~ShippedHeraldryBannerKeyTests"` if it exists on the tree: 0 failed. Data: no new ERROR.

Commit A5: `fix(banners): v2.0.28 - seven clan banners lose blank and debug layers`. Body: two Mirkwood houses drew a blank emblem and now fly the Mirkwood banner, Zorian of Rhun flies the Rhun banner, two Rivendell and Lothlorien houses lose two empty top layers, two Khand houses use their own colours; a test checks every icon and colour in every key. Trailers: `Not-tested: the banners in game`; `Ported-from:` `487e3d9a9`; `Refs #951`.

### Part A done criteria

- [ ] `dotnet test` full suite: 0 failed; total = A0 total plus the new tests.
- [ ] `python -m unittest discover -s tools/tests -t . -p "test_banner_icon_ids.py"`: `OK`, not skipped.
- [ ] `git log --oneline bannerlord-1.4.5..HEAD` prints 5 lines; `git diff --stat bannerlord-1.4.5...HEAD -- Main/Features Main/Adapters | tail -1` prints nothing.

---

## Part B: the schema gate and the validator codes (Python, hook; `tools/validate_moduledata.py` is serial)

WP108's collision passes come first (already merged); then B1's `SCHEMA_INVALID`; later codes after it. The 1.4.x file never took `ARMOUR_ACQUISITION_REF`, `CREATURE_GEAR_OBTAINABLE`, `SKILL_TEMPLATE_MISMATCH`, or the armour class passes, so never copy a whole 1.5.x `validate_moduledata.py` or `taom_schema.py`.

### Step B1: `validate_xml_schemas.py` and `SCHEMA_INVALID` (`fa8c06f82` tool half, `48d66b59a`, `4644bf493`, `c1ce78207`, tool hunks of `709649c3f`, `bc30b76da`, `09f3126a7`, `f1675c058`)

Method: end-state transplant of the tool and its test at `e45f6df2c` (no later change: the tool blob equals `origin/bannerlord-1.5.x`), then adapt. A replay would conflict in every step (simulated: `fa8c06f82` on `validate_moduledata.py`, its test, `.claude/hooks/check-moduledata-validation.sh`, `deep-review/SKILL.md`; the rest on the tool itself, absent here).

1. RED for the pass comes second; first the tool:
   ```bash
   git show e45f6df2c:tools/validate_xml_schemas.py > tools/validate_xml_schemas.py
   git show e45f6df2c:tools/tests/test_validate_xml_schemas.py > tools/tests/test_validate_xml_schemas.py
   ```
2. Adapt `tools/validate_xml_schemas.py`:
   - **D15, no allowlist.** Delete `ALLOWED_EXTRA_ATTRIBUTES` with its comment, `_UNDECLARED_ATTRIBUTE_RE` with its comment, `_is_allowed_extra_attribute`. In `validate()`: return `[f"L{e.line}: {e.message}" for e in schema.error_log]` directly; remove the `xml_id` sentence from its docstring and the parameter; drop the `xml_id` argument at its call in `run()`. Then delete `import re`: `_UNDECLARED_ATTRIBUTE_RE` was its only use at `e45f6df2c` (`grep -n '\bre\.' tools/validate_xml_schemas.py` prints nothing).
   - Docstring `:13`: replace "(v1.5.3 decompile," with "(v1.4.8, taom-src: `MBObjectManager.cs:877-917`, `:949`, `:1061-1129`, `XmlResource.cs:114-153`, `ModuleHelper.cs:232-247`;". Keep the method names.
3. Adapt `tools/tests/test_validate_xml_schemas.py`:
   - Delete class `TaomExtensionAttributeAllowlistTests` (`:337-408`, with its `@NEEDS_LXML`). Keep `MONSTERS_XSD` and `GOOD_MONSTER`. Add in its place:
     ```python
     @NEEDS_LXML
     class UndeclaredAttributeIsReportedTests(unittest.TestCase):
         """D15: the public Armory's Monster files carry taom_body_length, which v1.4.8 Monsters.xsd
         does not declare. It stays a reported failure (WP115's pairing check reads it as a signal)."""
         def test_taom_body_length_on_monster_fails(self):
             with tempfile.TemporaryDirectory() as tmp:
                 root = Path(tmp); xsd = root / "Monsters.xsd"; monster = root / "m.xml"
                 _write(xsd, MONSTERS_XSD); _write(monster, GOOD_MONSTER)
                 vx._schema.cache_clear()
                 errors = vx.validate(monster, xsd)
             self.assertEqual(len(errors), 1, errors)
             self.assertIn("taom_body_length", errors[0])
     ```
     If `_write`, `tempfile` or `Path` have other names at the top of the file, use those.
   - `LiveTaomMapRegistrationTests` (K: the 1.5.x pin `["Settlements"]` is #619 and false for the 1.4.x map): rename `EXPECTED_IDS` to `ALLOWED_IDS = {"Factions", "Items", "Kingdoms", "NPCCharacters", "SPCultures", "Settlements", "WorkshopTypes", "partyTemplates"}`, reword the docstring ("The live TAOM_Map is unversioned; a resync that adds a registration must show up here. The 1.4.x package registers these eight; the public package is UNVERIFIED"), and replace the last assert with `ids = {xml_id for _, xml_id in entries}; self.assertIn("Settlements", ids); self.assertLessEqual(ids, self.ALLOWED_IDS, sorted(ids - self.ALLOWED_IDS))`.
4. One-tool-test `test_validate_xml_schemas.py`: `OK`. `python tools/validate_xml_schemas.py; echo rc=$?`: `PASS: ... 0 failed, 0 registration(s) resolve to nothing`, rc=0. `python tools/validate_xml_schemas.py --live; echo rc=$?` is expected rc=1 (record the failures: on the public Armory, `taom_body_length` rows are D15's signal; `family_type` rows mean the v2.0.26 Armory). Never add anything to silence them.
5. RED for the pass, with a script (`import re` is there from WP108):
   ```python
   import sys
   sys.path.insert(0, "C:/Users/mikew/source/repos/taom-port/136-notes")
   from wp136_edit import load, save, once, cut, show
   p = "tools/tests/test_validate_moduledata.py"; t, nl = load(p)
   src = show("f1675c058:tools/tests/test_validate_moduledata.py")
   t = once(t, "import validate_moduledata as vm  # noqa: E402\n",
            "import validate_moduledata as vm  # noqa: E402\nimport validate_xml_schemas as vx  # noqa: E402\n")
   fixtures = cut(src, '_FACTIONS_XSD = """', "\n\n\nclass ArmourClassTableDriftTests")
   klass = cut(src, "class SchemaInvalidPassTests(unittest.TestCase):", "\n\n\nclass ItemSlotRejectedTests")
   t = once(t, "class CommitGateCoverageTests(unittest.TestCase):",
            fixtures + "\n\n\n" + klass + "\n\n\nclass CommitGateCoverageTests(unittest.TestCase):")
   save(p, t, nl)
   ```
   One-tool-test `test_validate_moduledata.py`: errors naming `schema_invalid_issues` or `SCHEMA_CODE`. If `SchemaInvalidPassTests` carries a test that needs code outside this plan, list it and STOP.
6. GREEN, `tools/validate_moduledata.py`, same style:
   - `v = once(v, "import sys\n", "import sys\nimport xml.etree.ElementTree as ET\n")` (only if not there; it lands after `import re` and `import sys`, before `from pathlib`).
   - Docstring: after the `COLLISION_BODY_BORROWED` rows append the `SCHEMA_INVALID` block from `fa8c06f82`'s hunk (quoted in `git show fa8c06f82 -- tools/validate_moduledata.py`).
   - Block: `cut(show("f1675c058:tools/validate_moduledata.py"), 'SCHEMA_CODE = "SCHEMA_INVALID"', '\n\n\nSLOT_CODE = ')` inserted before `def main() -> int:` (it carries `48d66b59a`'s repo-relative rerun hint).
   - `main()`: after `issues = validator.run()` insert the two lines
     `# The engine's XmlSchemas folder sits beside Modules in the install root.` and
     `issues += schema_invalid_issues(moduledata.parent, game_modules.parent / "XmlSchemas")`; move `issues.sort(key=lambda i: i.sort_key())` out of the `if` to after the whole `if/else`.
7. Hook `.claude/hooks/check-moduledata-validation.sh`: append `--code SCHEMA_INVALID` to the `--code` list (WP108's last row is `--code COLLISION_BODY_BORROWED \`). Timing comment: add one line "SCHEMA_INVALID adds about 0.5 s (1.5.x desktop, 2026-09-21)."
8. Rule `.claude/rules/moduledata-validation.md`: add the `SCHEMA_INVALID` row before `DUPLICATE_{NPC,CULTURE,ROSTER}_ID`, and the two Discipline bullets replacing "The validator does NOT enforce engine XSD required-attributes", from `git show fa8c06f82 -- .claude/rules/moduledata-validation.md`. Do not take its deep-review wording (the lens is WP138's). Do not add "Gate per file kind" yet (B7).
9. `docs/features/moduledata-validation.md`: add before `## Key Files` a section `## Engine XSD layer (`SCHEMA_INVALID`)`: the engine validates on load but only prints a failure (`MBObjectManager.ValidationEventHandler`, v1.4.8 `:1324`); `tools/validate_xml_schemas.py` mirrors its file and schema resolution; the pass covers the repo module, `--live` adds `TAOM_Map` and the Armory; a repeated `<Faction>`, `<Hero>` or `<Kingdom>` id is caught because the engine XSDs declare `@id` unique (`EngineIdUniquenessTests`, `b409e6505`); **`taom_body_length` is not allowlisted on this line (D15)**; `lxml` is optional (exit 2 without it, one NOT-verified warning from the pass). Key Files row for the tool and its test. Changelog line. `docs/modding/validation-and-testing.md`: the two table rows from `c1ce78207`.
10. Conditional (WP135's hand-off): if A0 found `_parse_like_engine` defined inside `tools/check_external_loc_coverage.py`, replace that local copy with `from validate_xml_schemas import _parse_like_engine   # noqa: E402` (the form at `e45f6df2c:tools/check_external_loc_coverage.py:41`) and run its test.

**Verify**: one-tool-tests for `test_validate_moduledata.py` and `test_validate_xml_schemas.py`: `OK`, including `CommitGateCoverageTests`. `python tools/validate_moduledata.py --code SCHEMA_INVALID; echo rc=$?`: `PASS`, rc=0. RED proof of the live path: temporarily delete `initial_home_settlement` from the first clan in `characters/clans.xml`, run the same command: 1 ERROR at that line, rc=1; `git checkout Main/_Module/ModuleData/characters/clans.xml`. Hooks: no new failure.

Commit B1: `chore(tools): v2.0.28 - check every engine-loaded file against its XSD`. Trailers: `Research: v1.4.8 MBObjectManager.GetMergedXmlForManaged, HandleXsltList, LoadXmlWithValidation, XmlResource, ModuleHelper`; `Not-tested: none (offline)`; `Constraint: D15, taom_body_length stays a reported failure`; `Ported-from:` `fa8c06f82`, `48d66b59a`, `4644bf493`, `c1ce78207`; `Refs #621 #974 #980`.

### Step B2: the item registry reads only `<Items>` documents (`1ebcf86a8`, K1 trap)

Simulated conflicts: `tools/validate_moduledata.py`, its test, `tools/README.md`. Take the `taom_schema.py` hunk only.

1. RED: in `BuildRegistriesTests` add the test below. It must not call `vm.armour_acquisition_ref_issues` or write armour_acquisition fixtures (that function is SKIP-FEATURE `04da4a0d0`, absent here):
   ```python
   def test_item_registry_counts_only_items_documents(self):
       # A config that quotes item ids in <Item id> rows must not define them, or no gate can fail on them.
       modules = Path(self._tmp.name) / "Modules"
       _write(modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "w.xml",
              '<?xml version="1.0"?>\n<Items>\n  <CraftedItem id="real_sword" crafting_template="OneHandedSword" />\n</Items>\n')
       _write(self.md / "taom_spcultures.xml",
              '<?xml version="1.0"?>\n<SPCultures>\n  <Culture id="gondor" />\n</SPCultures>\n')
       _write(self.md / "culture_marketplace" / "culture_marketplace_config.xml",
              '<?xml version="1.0"?>\n<CultureMarketplaceConfig>\n  <Blacklist><Item id="retired_helm" /></Blacklist>\n</CultureMarketplaceConfig>\n')
       _write(self.md / "own_items.xml",
              '<?xml version="1.0"?>\n<!-- TAOM\'s own items -->\n<Items>\n  <Item id="taom_own" Type="Goods" />\n</Items>\n')

       regs = ts.build_registries(self.md, modules)

       self.assertIn("real_sword", regs.items)
       self.assertIn("taom_own", regs.items, "TAOM's own <Items> file still defines its items")
       self.assertNotIn("retired_helm", regs.items, "a config row quoting an id is not a definition")
   ```
   Runs red on `retired_helm`.
2. GREEN `tools/taom_schema.py`: before `def _scan(` add `_DOC_ROOT_RE` and `_ITEM_DOC_ROOTS` with their comment (`git show 1ebcf86a8 -- tools/taom_schema.py`; rewrite the comment's example to the marketplace config and drop "RCA 2026-09-28 row 1"). Signature `def _scan(roots, pattern, want_files=False, doc_roots=None):`; after `text = _read_stripped(xml)` the four-line `doc_roots` check; items line `items, item_def_files = _scan(item_roots, _ITEM_DEF_RE, want_files=True, doc_roots=_ITEM_DOC_ROOTS)`.
3. `python tools/validate_moduledata.py 2>&1 | tail -3`. Compare per-code counts with A0. `BROKEN_ITEM_REF` may rise only for an id whose sole "definition" was a config row: list them. `git grep -n 'sk_mordor_mouth_of_sauron_helm' -- Main`: a blacklist row is not an `Item.` ref, so it should not appear as an issue. Anything else new: STOP.

**Verify**: one-tool-test `OK`.

Commit B2: `feat(tools): v2.0.28 - the item registry reads only Items documents`. Trailers: `Not-tested: none`; `Ported-from:` `1ebcf86a8`.

### Step B3: `BROKEN_FACTION_REF` and a crash exits 3 (`ca92db878`, adapted)

Simulated conflicts: `taom_schema.py`, `test_hooks.sh`, `test_validate_moduledata.py`, `validate_moduledata.py`, the hook, the rule, two docs. Adapt, do not pick. WP108's `_run_main` calls `vm.main()`, unchanged.

1. RED: in `tools/tests/test_validate_moduledata.py`: `import subprocess` is unnecessary unless `EntryPointCrashTests` uses it (it does at the `ca92db878` shape: add it). From `show("f1675c058:tools/tests/test_validate_moduledata.py")`, by `cut`: (a) the block `# Faction refs (#949)` through `test_faction_check_skipped_when_registry_unavailable` into `ValidatorContractTests` before `def test_extra_ref_roots_are_swept_and_reported_with_module_prefix`; (b) `_CLANS_XML` through `test_faction_stripped_by_spclans_xslt_is_not_registered` into `BuildRegistriesTests` before `def test_commented_definition_not_registered`; (c) `test_main_without_install_names_the_skipped_ref_checks` and `test_registry_line_counts_the_clans` at the end of `MissingCollisionBodyTests`; (d) class `EntryPointCrashTests`, through the line before `_FACTIONS_XSD`, before the fixtures B1 inserted. `tools/tests/test_taom_query.py`: `test_find_references_covers_faction_and_body_property_refs` from `git show ca92db878 -- tools/tests/test_taom_query.py`. The fixture's `setUp` may hold a `Registries` lacking `factions`: the new tests set it.
2. GREEN `tools/taom_schema.py` from `git show ca92db878 -- tools/taom_schema.py`: `factions` field at the end of `Registries`; the `faction` `RefKind` row (`{"neutral"}` allow); `_CLAN_DEF_RE`, `_CLAN_STRIP_RE` after `_BODYPROP_DEF_RE`; the `vanilla_factions` and `factions` lines in `build_registries` after `bodyprop_files` is complete and before `items, item_def_files = ...`; `factions = set()` in the no-install reset beside `body_properties = set()`; the two floors (`("factions", factions, 100)`, `("vanilla_factions", vanilla_factions, 50)`) with their comment; `factions=factions` in the return. Measured: 235 total, 90 vanilla after the strip, so neither floor warns.
3. GREEN `tools/validate_moduledata.py` from `git show ca92db878 -- tools/validate_moduledata.py`: docstring row and exit-code sentence; `import traceback`; wrap the imports and constants in the `try/except` with exit 3, keeping `DEFAULT_GAME_MODULES` exactly as it is; the longer no-install warning; `, {len(registries.factions)} clans` on the Registry line; `def entry()`; `sys.exit(entry())`.
4. `tools/taom_query.py` and `tools/taom_mcp_server.py`: the `body_property` and `faction` prefixes and the docstring words. `tools/taom_query.py:94`: "all six prefixes".
5. Hook: `ERR_FILE`, `2>"$ERR_FILE"`, the 124-path cleanup, the `RC -eq 3` allow block with `additionalContext`, `--code BROKEN_FACTION_REF`, from `git show ca92db878 -- .claude/hooks/check-moduledata-validation.sh`. Keep WP103 and WP116's reading of the command, `taom_git_repo_dir` lines and output shape. If the live deny does not use `hookSpecificOutput`, STOP (WP103 not merged). 1.5.x's kit-drift check is not on this line: drop `The kit check above ...` from the comment and read `RC` directly.
6. `tools/test_hooks.sh`: add the `VM_REPO` block from `git show ca92db878 -- tools/test_hooks.sh` (crash allow plus findings deny) just before the `head2 "Summary"` block at the end of the file (`git show ca92db878` places it before a `8. tools/context_budget_scan.sh` section head, which is absent on the 1.4.x tip; if an earlier WP brought that head, go before it instead). Add `pre_payload` above it only if `grep -c '^pre_payload()' tools/test_hooks.sh` prints `0`: copy the function from `git show ca92db878:tools/test_hooks.sh` (its `:1589`).
7. Rule row and `docs/features/moduledata-validation.md`: from the same commit's hunks: the `BROKEN_FACTION_REF` rule row, the exit-3 sentence, a short section. `docs/modding/open-questions.md` not taken (1.5.x wording).

**Verify**: one-tool-tests for `test_validate_moduledata.py`, `test_taom_query.py`: `OK`. `python tools/validate_moduledata.py --code BROKEN_FACTION_REF 2>&1 | grep -E 'clans|PASS'; echo rc=${PIPESTATUS[0]}`: the stderr `Registry` output shows `235 clans` or more, then `PASS`, rc=0. `bash tools/test_hooks.sh 2>&1 | grep -E 'validator (crash|findings)'`, no `FAIL`. Hooks: no new failure.

Commit B3: `feat(validator): v2.0.28 - every Faction reference must name a clan`. Trailers: `Research: 1,442 Faction refs over repo, Armory and TAOM_Map, 0 unresolved`; `Not-tested: none`; `Ported-from:` `ca92db878`; `Refs #949`.

### Step B4: `ITEM_SLOT_REJECTED` (`2420508fb`)

Simulated conflicts: test, validator, hook, rule. Adapt by block. WP117 brought the audit tool.

1. RED: `cut(show("2420508fb:tools/tests/test_validate_moduledata.py"), "class ItemSlotRejectedTests(unittest.TestCase):", "\n\n\nclass CommitGateCoverageTests")` before `class CommitGateCoverageTests`. Fails on `slot_rejection_issues`.
2. GREEN: docstring row; block `cut(show("2420508fb:tools/validate_moduledata.py"), 'SLOT_CODE = "ITEM_SLOT_REJECTED"', "\n\n\ndef main() -> int:")` after `schema_invalid_issues`; in `main()`'s install branch after `borrowed_body_issues`: `issues += slot_rejection_issues(game_modules, moduledata)`; in its `else` the `SLOT_CODE ... SKIPPED` warning. Hook: `--code ITEM_SLOT_REJECTED`.
3. `tools/apply_isengard_troop_revamp.py` (`:276`, `:283` write Cape; `troops_isengard.xml` already has Body): from `git show 2420508fb -- tools/apply_isengard_troop_revamp.py`, both rows to `("Body", "sk_uruk_hai_skirt_a1")` and the two-line comment.
4. Rule row and the doc section `## Item in the wrong slot` from the commit; cite v1.4.8 `Equipment.cs:204-220`. Its "Cost on the desktop" sentence: replace with your measured cost.

**Verify**: one-tool-test `OK`; `python tools/validate_moduledata.py --code ITEM_SLOT_REJECTED; echo rc=$?`: rc=0 expected (1.4.x `troops_isengard.xml` has Body). A finding is a real data row: report it and STOP (D13). `time python tools/validate_moduledata.py > /dev/null`: under the hook's 45 s budget; if over 35 s, STOP.

Commit B4: `fix(tools): v2.0.28 - refuse items written to a slot they cannot fill`. Trailers: `Research: v1.4.8 Equipment.DeserializeNode :204-220`; `Not-tested: none`; `Ported-from:` `2420508fb`; `Refs #729`.

### Step B5: `TROOP_ID_REMOVED`, `TOWNSFOLK_WITHOUT_BATTLE_ROSTER`, `INCONSISTENT_WEAPON_SLOT` (`f1675c058`, codes half)

The commit conflicts almost everywhere (13 paths); take these blocks by anchor. Leave: the docstring deletion (no codes doc here until WP138), `fords_of_war` glob, `generate_career_kits.py`, `audit_armory_refs.py`, `audit_fbx_lods.py`, `test_rebuild_name_tags.py`, `test_generator_regions_in_sync.py`, and `ItemTrade.item_type`.

Adaptation of `_weapon_slot_coverage`: **the ammo half is dropped**. It reads `reg.item_trade`, built by `build_item_trade` for SKIP-FEATURE `CREATURE_GEAR_OBTAINABLE`, absent on 1.4.x. Remove `trade = ...` and the whole `if trade:` block, and the sentence about ammo in the comment. Keep the no-common-slot and launcher halves. `launchers` exists (`build_launchers`, `:2208`).

1. RED: from `show("f1675c058:tools/tests/test_validate_moduledata.py")`: classes `WeaponSlotCoverageTests`, `TownsfolkBattleRosterTests`, `TroopIdRemovedTests`, `AppliesToCoverageTests` (`:2485-2756`) before `class MountWithoutHarnessTests`. In `WeaponSlotCoverageTests` delete `test_ammo_at_two_indices_warns`, `test_ammo_in_some_sets_only_warns`, and any `item_trade` line or `ItemTrade` use in its helper. `test_every_emitted_code_has_a_row_in_the_codes_doc` is **not** taken (WP138 creates the doc; hand-off below).
2. GREEN `tools/taom_schema.py`: `Validator.__init__` gains `shipped_troop_ids_file=None` and the assignment; `run()` gains the three calls after `_armour_slot_coverage`; the three method blocks from `f1675c058`'s hunk; module-level `read_shipped_troop_ids`, `write_shipped_troop_ids` before `build_launchers`' section. Check `_read`, `_rel`, `_xml_files` (`:242-275`) serve them unchanged.
3. GREEN `tools/validate_moduledata.py`: `SHIPPED_TROOP_IDS` inside the `try`; `regenerate_shipped_troop_ids`; the argparse flag and early return; `shipped_ids` and `shipped_troop_ids_file=shipped_ids` in the `Validator(...)` call (no `game_modules=` argument on this line). Docstring: three new rows. `tools/taom_query.py`: `_REPO_MODULEDATA`, `_SHIPPED_TROOP_IDS`, the `validate()` baseline lines. Hook: `--code TROOP_ID_REMOVED`.
4. Baseline: `python tools/validate_moduledata.py --regenerate-shipped-troop-ids bannerlord-1.4.5`. **Not** a tag: `v2.0.28` has 855 ids and this branch 857. **Verify**: prints `Wrote N troop ids`, N at least 857 (a smaller N: STOP); file header says `bannerlord-1.4.5`.
5. `tools/add_townsfolk_battle_rosters.py`: docstring hunk (`git checkout f1675c058 tools/add_townsfolk_battle_rosters.py`; no diff against `f1675c058^` at planning; check `git diff --stat f1675c058^ HEAD -- tools/add_townsfolk_battle_rosters.py` prints nothing first). Its text cites v1.5.6: the engine fact holds on v1.4.8 (`BasicCharacterObject.cs:108`).
6. `AppliesToCoverageTests`: if it names an uncovered file on this tree, report the file: a real `<NPCCharacters>` document gets its folder glob in `tools/schemas/taom_npccharacter.json`; anything else is a STOP.
7. Rule rows and doc lines for the three codes from `f1675c058`'s `.claude/rules/moduledata-validation.md`, `.claude/rules/troops.md` not taken (WP143). `docs/modding/equipment-rosters.md` not taken unless a sentence is needed to name the rule: leave it to WP144.

**Verify**: one-tool-test `OK`. `python tools/validate_moduledata.py --code TROOP_ID_REMOVED --code TOWNSFOLK_WITHOUT_BATTLE_ROSTER --code INCONSISTENT_WEAPON_SLOT 2>&1 | tail -4`: 0 errors, 0 townsfolk warnings. Report the `INCONSISTENT_WEAPON_SLOT` warning count (launcher half unmeasured at planning; warnings do not block; do not change data). `CommitGateCoverageTests` green.

Commit B5: `feat(tools): v2.0.28 - gates for removed troops and half-armed kits`. Trailers: `Constraint: the ammo half needs CREATURE_GEAR's item_trade, absent here`; `Research: v1.4.8 BasicCharacterObject.cs:108`; `Save-compat: none; a removed id would keep a hollow character in a save, which this gate refuses`; `Ported-from:` `f1675c058`; `Refs #295`.

### Step B6: small tool gates (`f1675c058` tools, K2 `569f6750d` comments)

1. `audit_mount_parity.py --strict`: `git diff --stat f1675c058^ HEAD -- tools/audit_mount_parity.py` prints nothing, then `git checkout f1675c058 tools/audit_mount_parity.py tools/tests/test_audit_mount_parity_strict.py`. Differs: hand-apply. `python tools/audit_mount_parity.py --strict | tail -6; echo rc=${PIPESTATUS[0]}`: `total` for all three, rc=0.
2. `apply_culture_skills_traits.py --check`: the file differs from `f1675c058^` by 19 lines (1.4.x has fewer), so by anchor: `_skill_set_rows` and `check_skill_sets` before `def update_skill_template_in_block`; the `--check` argument and early return in `main()` before `bios = load_bios()` (`:1275`). `git show f1675c058:tools/tests/test_apply_culture_skills_traits_check.py > tools/tests/test_apply_culture_skills_traits_check.py`. **Expected**: `python tools/apply_culture_skills_traits.py --check; echo rc=$?` prints `DRIFT`, `only in the generator (1): taom_canonical_lord_I1_0_skills`, rc=1. That is true drift. Report it. Do not run `--apply`. If the script's `__main__` ignores `main()`'s return, use `sys.exit(main())`.
3. `translate_with_claude.py --module` required. If A0's grep found `def main(argv` (WP135): `p.add_argument("--module", required=True, choices=["TAOM", "TAOM_Map", "Armory", "all"], help="Which module to translate; `all` also writes the live Armory (#1056)")` and the two usage lines from `git show f1675c058 -- tools/translate_with_claude.py`. If not found: also change `def main():` to `def main(argv=None):` and pass `argv` to `parse_args`. `git show f1675c058:tools/tests/test_translate_module_required.py > tools/tests/test_translate_module_required.py`. `git grep -n 'translate_with_claude.py --lang' -- tools .claude docs | grep -v -- --module`: after the usage lines above, the 1.4.x tip has no hit left in `tools/*.py` (`check_external_loc_coverage.py:145` and `harvest_literal_loc_keys.py:211` already pass `--module`); a hit in another `tools/*.py` file is a STOP, since it is out of scope. List the rest (the 1.4.x tip has `tools/README.md` and `docs/localization/TRANSLATOR_GUIDE.md`) for WP142.
4. K2 (`569f6750d`, comments only; the Uruk data is already on 1.4.x): `tools/analyze_kingdom_armour.py:139`, `tools/fix_upgrade_armour_regressions.py:235` and its "bare-chested Uruk-hai" block, `tools/taom_schema.py:1346`, the test docstring in `test_validate_moduledata.py`, the `MISSING_BODY_ARMOUR` rule row (`.claude/rules/moduledata-validation.md:83` on `1b00d1acd`): take the rewording from `git show 569f6750d -- <path>`. **Drop every `hill_troll` and "hill troll" word**: 1.4.x `_BODYLESS_BY_DESIGN` (`taom_schema.py:980-984`) has three entries and no `hill_troll`. No logic changes: `git diff -U0 -- tools/analyze_kingdom_armour.py tools/fix_upgrade_armour_regressions.py tools/taom_schema.py | grep '^[-+]' | grep -v '^[-+] *#' | grep -v '^[-+][-+]'` prints only docstring lines. The RCA `docs/reviews/rca-uruk-hai-skirt-body-2026-10-04.md`: copy it from `569f6750d` with every hill-troll sentence removed and a first-line note "1.4.x port note (plan 136): the data fix reached this line in `1b00d1acd`".

**Verify**: Tool tests: no new failure. Dash check on the RCA text you edited.

Commit B6: `chore(tools): v2.0.28 - strict mount parity, skill check, module flag`. Trailers: `Not-tested: none`; `Ported-from:` `f1675c058`, `569f6750d`; `Refs #1056`.

### Step B7: `tools/data_gates.py` (`f1675c058`, adapted; the WP138 K12 and K4 conditions)

1. RED: `git show f1675c058:tools/tests/test_data_gates.py > tools/tests/test_data_gates.py`. Fails on the import.
2. GREEN: `git show e45f6df2c:tools/data_gates.py > tools/data_gates.py` (not `origin/bannerlord-1.5.x`, which has moved). Then:
   - Remove every `Gate` row whose script is absent on the live tree. Print the candidates: `python -c "import sys,os;sys.path.insert(0,'tools');import data_gates as d;[print(g.id,g.cmd[1]) for g in d.GATES if g.cmd and g.cmd[0]=='python' and g.cmd[1]!='-m' and not os.path.isfile(g.cmd[1])]"`. At planning (before later WPs) these were `career-kits`, `name-loc`, `armour-classes`, `armory-names`, `restat-donors`, `troll-wiring`, `orc-locomotion`, `race-morphs`, `fbx-lods`, `map-fortifications`, `map-descriptions`, `banner-sheets`, `banner-rdc`, `rdc-entries`. Keep a row whose script an earlier WP landed (`name-loc`: WP135; `banner-sheets`: only if `tools/pack_banner_sheets.py` exists).
   - Also remove `generator-regions` (test file absent on this line), `troll-tests` (`git grep -l TrollBruteForceWiringTests -- TAOM.Tests` empty), `notable-templates` (gate not ported). `wanderers`: keep only if `git grep -l WandererBackstoryCoverageTests -- TAOM.Tests` hits.
   - `gui-tests`: keep only the names that `git grep -l` finds under `TAOM.Tests` (`PrefabExtensionBrushTests`, `PrefabSpriteTests` after E1). Keep the row itself: WP140's Step C6 adds its own names to it.
   - `CODES_DOC`: delete the constant and its use in `moduledata`'s `read_first` (WP138 re-adds).
   - `prefab-budget` note becomes "sums every module; see --all-modules" after D1 (edit it there). Docstring: delete "Lens 7 of /deep-review, /verify and the artist brief send authors here."; keep the rest, but "core.hooksPath" only if `.githooks` exists on this tree.
3. Adapt `tools/tests/test_data_gates.py`: planning ran it against the table filtered by script existence; beyond failures that vanish with this tree, these need edits: `test_the_armory_ladder_of_the_artist_brief_is_all_present`: reduce `wanted` to rows that survive; `test_register_names_runs_before_the_loc_coverage_gate`: delete if `armory-names` is gone; `test_the_banner_sprite_folder_selects_banner_sheets`, `test_the_install_folder_name_does_not_select_banner_sheets`: delete if `banner-sheets` is gone; `test_every_read_first_doc_exists`: passes after `CODES_DOC`; `test_a_troop_file_selects_the_moduledata_gates`: drop gate ids you removed from its expected set; `test_the_lord_skill_sets_and_their_generator_select_generator_regions`, `test_the_notable_template_test_is_routed_from_the_files_it_reads`: delete with their gates. `test_every_applies_to_glob_of_every_schema_selects_moduledata`: stays. Never loosen a verdict or selection assertion on a surviving gate.
4. Rule: add to `.claude/rules/moduledata-validation.md`, before `## Discipline`, the `## Gate per file kind` section verbatim from `git show e45f6df2c:.claude/rules/moduledata-validation.md` (`:38-46`), minus the sentence naming `/deep-review`'s lens unless `.claude/skills/deep-review/lenses/7-xml.md` exists.
5. `docs/features/moduledata-validation.md`: How-To line for the script.

**Verify**: one-tool-test `test_data_gates.py`: `OK`. `python tools/data_gates.py --list | head -8` prints the table. `python tools/data_gates.py tools/data_gates.py; echo rc=$?`: `no gate reads the changed files` or a gate list, never a traceback. `python tools/data_gates.py --all; echo rc=$?`: takes minutes (`timeout: 600000`); record the PASS, FAIL, NOT RUN table in the hand-off. FAILs that are this laptop's Armory vintage are expected until D20; never edit to quiet one.

Commit B7: `feat(tools): v2.0.28 - one command runs the data gates for what changed`. Trailers: `Not-tested: none`; `Ported-from:` `f1675c058`.

### Step B8: the validator header names each module's vintage (XML-DATA-02, new, TDD)

Finding: the 2026-11-04 lens needed this and the header did not show it: a stale Armory reads exactly like a repo defect. WP101 left this half here.

1. RED, in `ExtraRefRootTests` of `test_validate_moduledata.py`:
   ```python
   def test_vintage_line_names_version_and_newest_file_date(self):
       with tempfile.TemporaryDirectory() as tmp:
           mod = Path(tmp) / "LOTRLOME_Armory"
           _write(mod / "SubModule.xml", '<Module><Version value="v9.9.9" /></Module>')
           _write(mod / "ModuleData" / "a.xml", "<x />")
           os.utime(mod / "ModuleData" / "a.xml", (1767225600, 1767225600))
           line = vm.module_vintage(mod / "ModuleData")
       self.assertIn("LOTRLOME_Armory", line); self.assertIn("v9.9.9", line); self.assertIn("2026-01-01", line)

   def test_vintage_line_says_unknown_when_the_version_cannot_be_read(self):
       with tempfile.TemporaryDirectory() as tmp:
           mod = Path(tmp) / "M" / "ModuleData"; mod.mkdir(parents=True)
           self.assertIn("version unknown", vm.module_vintage(mod))
   ```
2. GREEN in `validate_moduledata.py`:
   ```python
   def module_vintage(root) -> str:
       """`<module>: <Version> (newest ModuleData file <date>)`. A stale copy reads exactly like a repo
       defect (XML-DATA-02), so every sweep says what it swept. Never raises: this is a header."""
       root = Path(root)
       try:
           version = ET.parse(root.parent / "SubModule.xml").getroot().find("Version").get("value")
       except (OSError, ET.ParseError, ValueError, AttributeError):
           version = "version unknown"
       newest = max((p.stat().st_mtime for p in root.rglob("*.xml")), default=None)
       stamp = datetime.fromtimestamp(newest, timezone.utc).date().isoformat() if newest else "no files"
       return f"{root.parent.name}: {version} (newest ModuleData file {stamp})"
   ```
   with `from datetime import datetime, timezone`, and in `main()` change the extra-root loop line to `print(f"Also sweeping refs in: {root} [{module_vintage(root)}]", file=sys.stderr)`.

**Verify**: one-tool-test `OK`; `python tools/validate_moduledata.py 2>&1 >/dev/null | grep 'Also sweeping'`: two lines, each with a version and a date.

Commit B8: `feat(tools): v2.0.28 - validator header names each module's vintage`. Trailer: `Refs #<new issue>`.

### Part B done criteria

- [ ] `python -m unittest discover -s tools/tests -t .`: no new failure vs A0.
- [ ] `bash tools/test_hooks.sh`: no new failure vs A0.
- [ ] `git grep -c -e SCHEMA_INVALID -e BROKEN_FACTION_REF -e ITEM_SLOT_REJECTED -e TROOP_ID_REMOVED -- .claude/hooks/check-moduledata-validation.sh` is at least `4`.
- [ ] `git grep -n taom_body_length -- tools/validate_xml_schemas.py` prints nothing.
- [ ] `git grep -n -e 'item_trade' -e 'ARMOUR_ACQUISITION' -e 'CREATURE_GEAR' -e 'fords_of_war' -- tools/validate_moduledata.py tools/taom_schema.py tools/schemas` prints nothing.
- [ ] `python tools/validate_moduledata.py`: no ERROR code beyond the WP101 baseline, stated in the hand-off with counts.

---

## Part C: one guard for generated XML blocks (XML-DATA-09)

Low value until a generator re-runs, cheap to land. Never run these generators against the data.

### Step C1: `_marker_region.py` and the kingdom generator (`f963c6188`, partial)

Simulated conflicts: `generate_arthedain_troops.py` and its test (leave them; Arthedain is 1.5.x content), `generate_new_faction_kingdoms.py` (hand). `bd457ee6e` reached this line in WP133 C5.

1. RED: `git show f963c6188:tools/tests/test_marker_region.py > tools/tests/test_marker_region.py`: fails on the import.
2. GREEN: `git show f963c6188:tools/_marker_region.py > tools/_marker_region.py`. In `tools/generate_new_faction_kingdoms.py`: add `import sys`, `sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))`, `from _marker_region import replace_region  # noqa: E402` after the imports (`:10-11` on `1b00d1acd`), and reduce `upsert` to its comment plus `return replace_region(text, marker, f"  <!-- {marker}:BEGIN -->\n{payload}  <!-- {marker}:END -->\n", close_tag)`.

**Verify**: one-tool-tests `test_marker_region.py` and `test_generate_new_faction_kingdoms.py`: `OK`.

Commit C1: `fix(tools): v2.0.28 - one guard for generated XML blocks`. `Ported-from:` `f963c6188`; `Refs #975`.

### Step C2: the two culture writers (`c73cf2961`, partial)

1. RED: `git show c73cf2961:tools/tests/test_region_writers_in_place.py > tools/tests/test_region_writers_in_place.py`: fails (`refuse_unparsable`, `unparsable`).
2. GREEN: `tools/insert_new_factions.py` is identical to `c73cf2961^` (checked): `git checkout c73cf2961 tools/insert_new_factions.py` after `git diff --stat c73cf2961^ HEAD -- tools/insert_new_factions.py` prints nothing.
3. `tools/promote_borrowed_cultures.py` by hand from `git show c73cf2961 -- tools/promote_borrowed_cultures.py`: docstring lines; `from _marker_region import replace_region`; `unparsable` after `write` (`:158`); `upsert` (`:300-...`) reduced as the hunk does; the comment in the duplicate check; in `main()`, the `bad = unparsable(pending_writes)` refusal right after the `-> N file writes` print. If the live `main()` has no `pending_writes` list (it writes as it goes), STOP and report: the hand port needs a redesign. It must keep `--dump` and any live mode as they are.
4. Do not take `test_generator_regions_in_sync.py`.

**Verify**: one-tool-test `test_region_writers_in_place.py`: `OK`. `git diff --stat -- Main` prints nothing.

Commit C2: `fix(tools): v2.0.28 - culture generators keep blocks, never half-write`. `Ported-from:` `c73cf2961`; `Refs #976 #979`.

### Part C done criteria

- [ ] Tool tests: no new failure. `git grep -n -F "re.sub(r'[ \t]*<!-- ' + re.escape(marker)" -- tools/insert_new_factions.py` prints nothing (it matches line 153 on the 1.4.x tip).
- [ ] `git grep -n replace_region -- tools/generate_new_faction_kingdoms.py tools/insert_new_factions.py tools/promote_borrowed_cultures.py` prints at least 3 uses.

---

## Part D: the tools the lens flagged, the mbproj audit, the RCA follow-up gate

### Step D1: prefab budget counts every module (XML-DATA-05, new, TDD)

Evidence (lens, re-read this session in `tools/check_prefab_budget.py`): the default path is the desktop's, so on the laptop it prints `ERROR: not a directory` and returns 2; it counts only `TAOM_Map`. Measured by the lens: TAOM_Map 93,830; all installed modules 130,468 of 131,072 (604 spare); the players' set without NavalDLC about 120,601. The cap is in the wEditor native DLL only, so this is a Modding Kit hazard, not a player one.

1. RED, new `tools/tests/test_check_prefab_budget.py` using a temp `Modules` tree with `A/Prefabs/x.xml` (2 `<game_entity>`) and `B/Prefabs/y.xml` (3): `python tools/check_prefab_budget.py --all-modules --modules <tmp>` prints `TOTAL: 5` and per-module lines, exit 0; a tree over `CAP` exits 2; over `WARN` exits 1; `--dir` keeps today's single-folder behaviour; no args with a missing install exits 2 and names the path. Monkeypatch `CAP`/`WARN` by calling a new `verdict(total, cap, warn)`.
2. GREEN: `from _gamedir import game_modules`; default `Modules` through `game_modules(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")`; `--all-modules` sums `*/Prefabs/*.xml`, prints each module's count; **summing is the default when `--dir` is not given**, since TAOM_Map alone reads OK at 99.5% of the cap. Docstring: state that the cap asserts in the Modding Kit (wEditor) only and that NavalDLC decides the margin.

**Verify**: one-tool-test `OK`. `python tools/check_prefab_budget.py; echo rc=$?` on the laptop: a total (lens measured 130,468 with NavalDLC), rc=1 (over `WARN`). rc=1 here is the truth, not a failure.

Commit D1: `fix(tools): v2.0.28 - prefab budget sums every module, finds the install`. Trailer: `Refs #<new issue>`.

### Step D2: scene-name audit skips the file the engine never loads (XML-DATA-10, new, TDD)

Evidence: `TAOM_shadow` (`Modules/TAOM/ModuleData/settlements.xml`) yields 9 false `CRASH SUSPECTS`; the installed TAOM `SubModule.xml` registers no `Settlements` XmlName (lens).

1. RED, new `tools/tests/test_audit_scene_names.py`: `registers_settlements(module_dir)` is `True` for a `SubModule.xml` with `<XmlName id="Settlements" path="settlements"/>`, `False` without, `False` for a missing file.
2. GREEN: add it (ElementTree; a parse error returns `True` so a damaged manifest never hides a suspect); in `main()` give a `SETTLEMENT_FILES` row whose module does not register `Settlements` an empty `refs[label]` (the code below reads `refs["vanilla_SandBox"]` and every label by key) and print `<label>: skipped, its module registers no Settlements (the engine never loads this file)` in place of its `distinct scene_names` line. `NavalDLC` registers it (its `XmlName` spans several lines, so parse, never grep). Leave the other rows alone.

**Verify**: one-tool-test `OK`; `python tools/audit_scene_names.py | grep -c TAOM_shadow`: 1 (the skip line), and no `[TAOM_shadow] ... missing` block.

Also the remaining XML-DATA-10 noise: add one sentence to `docs/features/moduledata-validation.md` ("Expected noise in a player's rgl log, not a data defect: `Unable to find particle system with name ...` (vanilla names), `Unable to find item to add dependency` while Native's packages register (UNVERIFIED without a vanilla-only log)"). `taom_body_length` lines are not noise on this line: WP115 reports them (D15).

Commit D2: `fix(tools): v2.0.28 - scene audit skips a settlements file no one loads`. Trailer: `Refs #<new issue>`.

### Step D3: mbproj audit by type (`bc30b76da`, tool half, v1.4.8 re-derived)

Simulated conflicts: `.gitattributes`, `lint_docs.py`, `test_hooks.sh`, the schema tool, and bookkeeping. The audit file itself merges. Method: take the blobs at `bc30b76da` here; `09f3126a7`'s exact-id table follows in Step D3b.

1. RED: `git show bc30b76da:tools/tests/test_audit_mbproj_registration.py > tools/tests/test_audit_mbproj_registration.py`: fails on the audit's old API.
2. GREEN: `git show bc30b76da:tools/audit_mbproj_registration.py > tools/audit_mbproj_registration.py`. It imports `_parse_like_engine, mbproj_rows` from B1's tool. Adapt:
   - Delete `"soln_worldmap_color_grades",` from `MANAGED_IDS` and its comment lines and the docstring's mention: nothing in the v1.4.8 dump reads it. If a test asserts on it, adjust that one test.
   - Replace `(v1.5.5)` in the docstring and comment with `(managed half verified on v1.4.8, MBObjectManager.cs:920-946 and the callers listed; NATIVE_BY_TYPE comes from v1.5.5 disassembly of FUN_180719810 and is UNVERIFIED on v1.4.8, whose native DLL holds all thirteen strings)`.
3. Snapshot and attribute: copy `$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData/project.mbproj` byte for byte over `docs/reference/lotrlome-armory-snapshot/project.mbproj`, only if A0's Armory version is the public one (D20); else leave the snapshot and note it for the maintainer. Add to `.gitattributes` beside the howdah snapshot rule, or at the end if that rule is absent: the two-line comment and `docs/reference/lotrlome-armory-snapshot/project.mbproj text eol=lf` (planning found the 1.4.x tip's snapshot 38 lines, the laptop's live file 50). Do not copy 1.5.x's snapshot.
4. Modding docs: not taken here: the sentences "native reads a row's type" depend on the UNVERIFIED native check; hand-off to WP144 after a v1.4.8 native check.

**Verify**: one-tool-test `OK` (36 at planning). `python tools/audit_mbproj_registration.py; echo rc=$?`: 0 errors, the `soln_voice_definitions` warning, rc=0. A new ERROR on the public Armory: report the row, STOP.

Commit D3: `fix(tools): v2.0.28 - mbproj audit reads rows as the engine does`. Trailers: `Research: v1.4.8 MBObjectManager.GetMergedXmlForNative, XmlResource.GetMbprojxmls, requester grep`; `Constraint: NATIVE_BY_TYPE is v1.5.5 data, unverified on 1.4.8`; `Ported-from:` `bc30b76da`; `Refs #983`.

### Step D3b: the exact requested-id table (`09f3126a7`, audit half; take it if it ports cleanly, otherwise record the skip)

This half belongs to this WP (programme decision; WP138 and WP144 point here). `bc30b76da` treats vanilla Native's own `soln_*` ids as a stand-in, so a row with a vanilla id nothing requests reads clean. `09f3126a7` replaces the stand-in with a table, adds `INERT-ROW`, and makes a partial run exit 2.

Evidence at planning (read, not run):
- Its imports `_key`, `_parse_like_engine`, `_registrations`, `mbproj_rows` are all in B1's `e45f6df2c` tool. Its test file (87 tests) equals the `e45f6df2c` blob; the tool at `e45f6df2c` differs only in the comments of `12b010626`.
- Managed requesters on v1.4.8 (taom-src): `Module.cs` `CreateProcessedSkinsXMLForNative` `:1363`, `ItemHolsters` `:1375`, `ActionSets` `:1387`, `ActionTypes` `:1416`, `Animations` `:1427`, `VoiceDefinitions` `:1447`, `SoundEventData` `:1479`, `SoundParams` `:1490`, `ModuleData` `:1501`; `MBMusicManager.cs:345`; `Game.cs:437, :439`; `new StreamReader` at `XmlResource.cs:121`.
- Three table rows do not carry over as written. `soln_worldmap_color_grades`: no managed reader in the v1.4.8 dump, and the string is not in the installed native DLL, so it leaves. `soln_animations` and `soln_sound_event_data`: `09f3126a7` drops them on v1.5.5 native-slot evidence; their callbacks exist on v1.4.8 and those slots were not read here, so they stay, which can only avoid a false `DEAD-ID`.
- The five native ids (`soln_movement_sets`, `soln_full_movement_sets`, `soln_bone_body_types`, `soln_monster_usage_sets`, `soln_item_usage_sets`): each `xmlType` string is in the installed native DLL; the callers are UNVERIFIED on v1.4.8, the same standing as `NATIVE_BY_TYPE`. So the v1.4.8 table has 14 rows.
- Rows on this laptop (Armory v2.0.26): the four Armory `soln_monsters` rows have `<XmlName id="Monsters">` twins, so they read `INERT-ROW`; every other Armory and TAOM row is requested or has a native type; `TAOM_Map`'s `project.mbproj` holds `<Module>` rows, which `mbproj_rows` does not read. Expected: 0 errors.

1. RED: `git show 09f3126a7:tools/tests/test_audit_mbproj_registration.py > tools/tests/test_audit_mbproj_registration.py`. These edits only:
   - `EXACT_REQUESTED`: remove `"soln_worldmap_color_grades"`; add `"soln_animations", "soln_sound_event_data"`.
   - Rename `test_the_table_is_exactly_the_thirteen_requested_ids` to `test_the_table_is_exactly_the_requested_ids`.
   - `test_every_id_names_its_source`: delete the `MapColorGradeManager` assert.
   - Delete `test_the_ids_of_the_uncalled_callbacks_are_not_requested`.
   - Replace `test_worldmap_color_grades_is_requested` with:
     ```python
     def test_worldmap_color_grades_is_dead_on_v1_4_8(self):
         # Nothing in v1.4.8 reads it; vanilla Native still registers the row.
         findings = self.run_audit(
             self.wrap('<file id="soln_worldmap_color_grades" name="ModuleData/a.xml"/>'))
         self.assertEqual(1, len(self.errors(findings, "DEAD-ID")), findings)
     ```
   One-tool-test: fails (no `REQUESTED_IDS` yet).
2. GREEN: `git show 09f3126a7:tools/audit_mbproj_registration.py > tools/audit_mbproj_registration.py`. Edit:
   - `REQUESTED_IDS`: delete the `soln_worldmap_color_grades` entry. Replace the `# Not listed: ...` comment with two rows, `"soln_animations": "Module.CreateProcessedAnimationsXMLForNative (Module.cs:1427)"` and `"soln_sound_event_data": "Module.CreateProcessedSoundEventDataXMLForNative (Module.cs:1479)"`, under the comment `# 1.5.x dropped these two on v1.5.5 native-slot evidence, not read on v1.4.8, so they stay here.`
   - Re-cite to the v1.4.8 lines above: the six `Module.cs` rows, `Module.cs:1584` to `Module.cs:1501` in the five native rows, `MBMusicManager.cs:351` to `:345`, `XmlResource.cs:92` to `:121`.
   - Docstring: first line's `(v1.5.5)` becomes D3's wording (managed half verified on v1.4.8, native half v1.5.5 disassembly and UNVERIFIED on v1.4.8). In the table paragraph say 14 ids (eight `Module.CreateProcessed*XMLForNative`, `soln_soundtrack`, the five native ids); drop `soln_worldmap_color_grades` and the "Nothing in v1.5.5 calls" sentence. The `# The ids ... (v1.5.5)` comment takes the same wording. In `is_official`'s docstring delete " on v1.5.5" and in `main()`'s `Requested ids:` line replace "the exact set the engine asks for" with "v1.4.8 managed ids plus five native ids, UNVERIFIED".

**Verify (the clean-port test)**: `grep -n 'worldmap\|v1\.5' tools/audit_mbproj_registration.py`: no `worldmap`; each `v1.5` line says the native half is UNVERIFIED on v1.4.8 or names the 1.5.x evidence. One-tool-test: `OK`, no test edited beyond step 1. `python tools/audit_mbproj_registration.py; echo rc=$?`: 0 errors, rc=0; warnings are the `INERT-ROW` rows, the `soln_voice_definitions` row, and possibly `DEPLOYED-DIFFERS` (a stale deploy on this laptop, not a defect).

**If it does not port cleanly** (a test needs more than step 1, the tool needs a name B1's tool lacks, or an ERROR D3's run did not show): the table's native half is UNVERIFIED here, so a new `DEAD-ID` cannot be told from a false one. Run `git checkout HEAD tools/audit_mbproj_registration.py tools/tests/test_audit_mbproj_registration.py`, make no commit, and write in the hand-off: `09f3126a7 audit half: skipped, reason: <failing test or row>`. This is the decided outcome, not a STOP. D3's tool stays.

Commit D3b: `fix(tools): v2.0.28 - mbproj audit checks the ids v1.4.8 requests`. Stage `tools/audit_mbproj_registration.py` and `tools/tests/test_audit_mbproj_registration.py`. Body: a row must now name an id something asks for or a type native reads; a monster row that loads through `SubModule.xml` is a warning; a run that audits too little exits 2. Trailers: `Research: v1.4.8 Module.cs requesters, MBMusicManager.cs:345, Game.cs:437`; `Constraint: the five native ids and NATIVE_BY_TYPE are v1.5.5 data, unverified on 1.4.8`; `Not-tested: none`; `Ported-from:` `09f3126a7`; `Refs #987`.

### Step D4: RCA FOLLOW-UP gate in the doc linter (`bc30b76da`, lint and hook half; WP138 K2, K3)

1. RED in `tools/tests/test_lint_docs.py`: from `git show bc30b76da:tools/tests/test_lint_docs.py`, classes `_GitRepo`, `RcaFollowUpScannerTests`, `RcaFollowUpGateTests` (drop `AiDashGitScopeTests` only if it fails for a reason outside these hunks, and name it). A test that needs `check_context_budget` (absent on 1.4.x): adapt it to `check_claude_md_budget`'s report or delete it and name it in the hand-off.
2. GREEN `tools/lint_docs.py` from the same commit: `rca_followups` field and total; `_changed_markdown_lines(base, staged=False, pathspec="*.md")`; `_FOLLOWUP_RE`, `_ISSUE_REF_RE`, `_NOT_AN_ISSUE_RE`, `_NO_ISSUE_RE`, `_ISSUE_LINK_TARGET_RE`, `_RCA_PATHSPEC`, `_is_rca_doc`, `_followup_has_owner`, `scan_text_for_followups`, `check_rca_followups`; the report line and section (match the separator style of the neighbouring report lines; a long dash in a code string of that file is existing style, not prose); `--staged`; both call sites in `main()`; the summary counter (`--drift-only`'s keep-list from WP103 gains `rca_followups`); the exit condition. On this line `--fail-on-drift`'s gate is `check_claude_md_budget`, not ADR-011: keep it.
3. Hook `.claude/hooks/check-doc-config-drift.sh`: add `docs/reviews/rca-*.md) RELEVANT=1; break ;;` to the relevance loop; `--drift-only --staged`; add `RCA FOLLOW-UP lines with no issue or reason` to the section names the live `awk` extracts (or take the 1.5.x generic awk if the live one names sections and fails to quote this one). Keep WP103's output contract.
4. `tools/test_hooks.sh`: add a small case near the existing drift tests: a sandbox repo staging only `docs/reviews/rca-x-2026-10-23.md` makes the hook run a stub `tools/lint_docs.py` recording its arguments (`--drift-only --staged`) and deny on its rc 1 with the section text; staging `docs/reviews/notes.md` does not run it. Model it on `bc30b76da`'s block (`git show bc30b76da -- tools/test_hooks.sh`); its `drift_run` and `PRJ_WT` helpers exist only if WP103 or WP116 brought them (`grep -c 'drift_run' tools/test_hooks.sh`); otherwise write the sandbox inline.
5. `docs/features/doc-health-linter.md`: the check, `--staged`, the rule. `.claude/skills/deep-review/SKILL.md` not taken (WP138).

**Verify**: one-tool-test `test_lint_docs.py`: `OK`. `python tools/lint_docs.py --drift-only --staged --summary; echo rc=$?`: `rca_followups: 0`, rc=0 (when nothing else gates). Hooks: no new failure. Dash check.

Commit D4: `feat(tools): v2.0.28 - a bare FOLLOW-UP in an RCA fails the doc gate`. Trailers: `Not-tested: none`; `Ported-from:` `bc30b76da`; `Refs #984`.

### Part D done criteria

- [ ] Tool tests and hooks: no new failure vs A0.
- [ ] `git grep -c 'REQUESTED_IDS' -- tools/audit_mbproj_registration.py` is non-zero, or the hand-off records D3b's skip with its reason.
- [ ] `git grep -c 'def check_rca_followups' -- tools/lint_docs.py` prints `1`; `python tools/lint_docs.py --help | grep -c -- --staged` at least `1`.
- [ ] `git grep -n 'E:\\Steam' -- tools/check_prefab_budget.py` shows one line, the `game_modules(...)` fallback.

---

## Part E: architecture gates (C#)

### Step E1: entry points, service signatures, service locator, sprites, #371 pairing (`8caac7da5`, adapted)

Simulated conflicts: `.claude/rules/csharp-architecture.md`, `.claude/skills/release/SKILL.md`, `AGENTS.md`, `NotableTemplateGenderTests.cs`, `PrefabExtensionBrushTests.cs`, two ADRs, `release-process.md`. Do not pick. Take the test files by blob:

```bash
for f in EntryPointLineCountGateTests ServiceSignatureGateTests ServiceLocatorGateTests PrefabSpriteTests; do git show 8caac7da5:TAOM.Tests/Infrastructure/$f.cs > TAOM.Tests/Infrastructure/$f.cs; done
```

Leave: `NotableTemplateGenderTests` (v1.5.0 Trader start), the ADR edits (the maintainer's, Step E3), rules, `AGENTS.md`, release skill (WP143 has conditional lines for these gates).

1. `ServiceSignatureGateTests.cs` (K1 trap): `TaomAssemblies` does not exist on 1.4.x (SKIP-FEATURE `a15563a3c`). Replace each `Scan(TaomAssemblies.All)` with `Scan(TaomAssembly)` and add `private static readonly Assembly[] TaomAssembly = { typeof(TAOM.Adapters.IAgentVisualsAdapter).Assembly };`. If that interface is not on the tree, use any public type of `Main` (`git grep -l 'public interface I.*Adapter' -- Main/Adapters | head -1`). Summary: "the compiled TAOM assembly", delete "(core and client)". `MaxBaselineEntries`: set after step 5.
2. `EntryPointLineCountGateTests.cs`: summary "in Main or Client" becomes "in Main"; the `Assert` message "Main and Client sources" becomes "Main sources". `RepoPaths.CSharpFiles()` already means `Main` here (WP104). A hard-coded `FordsEntryPointLineCountTests` mention: delete it. `MaxBaselineEntries`: step 5.
3. `ServiceLocatorGateTests.cs`: summary and failure text "Main or Client" become "Main"; delete the two `DataRow("Client/...")` rows. A grep of the tip finds `IoC.Resolve` in scope only inside comments (`CharacterCreationContentService.cs:35, :245`, `FieldCampOverlayVM.cs:14`, `SupplyOrderScreenVM.cs:12`), and `Calls` strips comments, so it starts at zero. A hit after earlier WPs: that WP broke the rule; STOP and report it.
4. `PrefabSpriteTests.cs` and `PrefabExtensionBrushTests.cs`: refactor the latter as the hunk shows: `internal static string GameDir()`, `internal static IEnumerable<string> ModuleFolders(params string[] under)`, `DeclaredBrushes()` keeping its name and calling `ModuleFolders("GUI", "Brushes")`, the Inconclusive text using `GameDir()` (read `git show 8caac7da5 -- TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs`; WP131's file is otherwise the base; WP140 relies on `DeclaredBrushes()` staying). In `PrefabSpriteTests`: delete the `AllowedNonDeclaredNames` entry and comment only if the name is unused on this tree (NavalDLC is not loaded on 1.4.x either; keep the set if a prefab still names it); keep the `TheKnownSet_...` assertion that a NavalDLC sprite is unknown only if `Map\anchor_stroked` is in NavalDLC's data on v1.4.8 (the test tells you). An unknown sprite on this tree is a real finding: list it, and if under five, add it to `AllowedNonDeclaredNames` with a comment saying why, and name it in the hand-off; more: STOP.
5. Baselines, from this tree:
   ```bash
   TAOM_WRITE_ENTRY_POINT_BASELINE=1 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests.Baseline_Regenerate_WhenAsked"
   TAOM_WRITE_SERVICE_SIGNATURE_BASELINE=1 dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~ServiceSignatureGateTests.Baseline_Regenerate_WhenAsked"
   ```
   Never copy 1.5.x's `Baseline.txt` files (their 18 and 113 rows describe 1.5.x code). Set both `MaxBaselineEntries` constants to the counts the regenerated files hold (`# Entries: N` and the row count). The test writes into the source tree through `RepoPaths`; check both files landed under `TAOM.Tests/Infrastructure/` and that `git status` shows them.
6. `BundledDependencyManifestTests.cs`: add `DependenciesPairing_MainMetadataEqualsDependenciesVersion` from the hunk before `ReadCoopModulesSection`'s summary (hand merge; `MainSubModule` and `RepoRoot` exist at `:27-31`; `using System.Xml.Linq`, `System.Linq` if absent). Passes today: `Main/_Module/SubModule.xml:30` pins `v2.0.6`, `Dependencies/_Module/SubModule.xml:6` is `v2.0.6`. If WP115 or WP118 already added an equal test, keep theirs and skip this.
7. `.claude/rules/csharp-architecture.md`, `gui-ui.md`, `adapters.md`: left to WP143 (it has the condition).

**RED proofs** (throwaway, reverted before commit): lengthen a small entry point past 150 lines: `EntryPoints_StayUnder150Lines...` fails. Add `IoC.Resolve<object>();` to a `*Service.cs` outside a comment: `NoServiceOrViewModel_CallsIoCResolve` fails. `git checkout <files>` both.

**Verify**: Build; Filtered `"FullyQualifiedName~EntryPointLineCountGateTests|FullyQualifiedName~ServiceSignatureGateTests|FullyQualifiedName~ServiceLocatorGateTests|FullyQualifiedName~PrefabSpriteTests|FullyQualifiedName~PrefabExtensionBrushTests|FullyQualifiedName~DependenciesPairing"`: 0 failed (the two `Baseline_Regenerate_WhenAsked` report Inconclusive without their variables). `git grep -n -e TaomAssemblies -e 'Client/' -- TAOM.Tests/Infrastructure/ServiceSignatureGateTests.cs TAOM.Tests/Infrastructure/ServiceLocatorGateTests.cs TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs`: nothing.

Commit E1: `test(gates): v2.0.28 - gates for entry points, services and sprites`. Body: rules that docs claimed now hold for anyone who runs the suite; both baselines list this tree's older offenders and only shrink. Trailers: `Constraint: one assembly on this line; no TaomAssemblies`; `Not-tested: none`; `Ported-from:` `8caac7da5`; `Refs #371`.

### Step E2: PatchShield finalizer comment and its pin (`48d66b59a`, shared hunk)

WP107 ported `5ee49d05f`; after it, 1.5.x changed `PatchShield.cs` only by this comment. Harmony is 2.4.2 on this line (`Dependencies/TAOM.Dependencies.csproj:70`).

1. RED: in `TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs` add the `SwallowPostfixWithResult` target, its `_foreign.Patch` postfix, its `Shield` line and the new test method from `git show 48d66b59a -- TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs`. It pins behaviour, so it passes at once if Harmony behaves as the comment says: that is the proof. A failure: STOP (the comment would be false on this line).
2. Replace the `ShieldFinalizerWithResult` summary with the text from `git show 48d66b59a -- Dependencies/Foundation/PatchShield.cs`, keeping any line WP107 added. Comment only: `git diff -U0 -- Dependencies/Foundation/PatchShield.cs | grep '^[-+]' | grep -v '^[-+] *///' | grep -v '^[-+][-+]'` prints nothing, so the #371 pairing is untouched.

**Verify**: Build; Filtered `PatchShieldFinalizerTests`: 0 failed.

Commit E2: `fix(dependencies): v2.0.28 - say what a swallowed postfix fault returns`. `Ported-from:` `48d66b59a`.

### Step E3 (the maintainer, not the executor): the ADR text `8caac7da5` changes

The config-protection hook blocks agents from `docs/adrs/`, so the executor stops before this and names it in the hand-off. The maintainer runs it in a terminal in the WP worktree after E1, before the integrator merges the branch. Why by hand: `8caac7da5`'s diff sits on 1.5.x's `250e1b321` amendment text, which this line does not have (WP143 Part G carries that amendment), so it does not apply. The edit below is its 1.4.x equivalent. Every anchor occurs exactly once at both `1b00d1acd` and `ec5a19270` (the base of WP127's Part M), so it needs no ordering against WP127. The `### Amendment (2026-11-03)` hunks and the `/deep-review` lens wording are left out: WP143 and WP138 own those. The class facts were read on v1.4.8 with taom-src.

```python
import sys
sys.path.insert(0, "C:/Users/mikew/source/repos/taom-port/136-notes")
from wp136_edit import load, save, once
EDITS = {
 "docs/adrs/002-thin-entry-points.md": [
  ("2. **Architecture Tests**: Automated tests verify entry point line counts",
   "2. **Architecture Tests**: `EntryPointLineCountGateTests` fails an entry point of 150 lines or more, against the shrink-only list of older classes in `EntryPointLineCountBaseline.txt`. `ServiceLocatorGateTests` fails an `IoC.Resolve` in a service, a view model or an engine."),
 ],
 "docs/adrs/007-adapter-pattern.md": [
  ("Service layer MUST use adapter interfaces for all sealed TaleWorlds game classes.",
   "Service layer MUST use adapter interfaces for all TaleWorlds game classes (sealed or not), in public, internal and protected signatures alike."),
  ("TaleWorlds game classes are sealed, preventing:",
   "Most TaleWorlds game classes are sealed, and the rest are public classes or static classes that a test cannot substitute either. Any of them prevents:"),
  ("**Sealed Classes Requiring Adapters**:",
   "**Classes Requiring Adapters** (`TroopRoster`, `ItemRoster`, `Equipment`, `WeaponComponentData`, `ArmorComponent`, `Town`, `Village` and `Hideout` are public classes, not sealed; the three Action types are static; `CampaignTime`, where the list still names it, is a struct; the rest are sealed):"),
  ("- `Settlement` and `TroopRoster` are sealed, cannot be mocked",
   "- `Settlement` is sealed and `TroopRoster` is a public class; neither can be mocked"),
  ("### Architecture Tests (Recommended)", "### Architecture Tests"),
  ("See `TAOM.Tests/Architecture/AdapterPatternArchitectureTests.cs` for automated enforcement.",
   "`ServiceSignatureGateTests` fails a public, internal or protected member of a `*Service` type or an `I*Adapter` interface that names a TaleWorlds class (sealed or not), against the shrink-only list in `ServiceSignatureBaseline.txt`."),
 ],
}
for path, pairs in EDITS.items():
    t, nl = load(path)
    for old, new in pairs:
        t = once(t, old, new)
    save(path, t, nl)
print("ok")
```

**Verify**: prints `ok`; `git diff --numstat -- docs/adrs/002-thin-entry-points.md docs/adrs/007-adapter-pattern.md` prints `1 1` and `6 6`, and no other `docs/adrs` path changes. `STOP: anchor found 0 times` means an earlier WP reworded the line: edit that line by hand to say the same thing.

Commit E3 (the maintainer): `git add docs/adrs/002-thin-entry-points.md docs/adrs/007-adapter-pattern.md`, then `git commit -F <msgfile>` as its own command, subject `docs(adr): v2.0.28 - ADR-002 and ADR-007 name the gates that hold them`. Body: the two ADRs promised tests that did not exist; they now name the gates from E1, and ADR-007 stops calling every class sealed. Trailer: `Ported-from: 8caac7da5df694fbdebc0ec4581eb4ffc0ef1cfc`.

### Part E done criteria

- [ ] Full `dotnet test`: 0 failed; total = the count the previous merged WP recorded in `plans/README.md` plus every new test of Parts A and E.
- [ ] `git status --short` clean; `git diff --name-only bannerlord-1.4.5...HEAD | grep -v -f <(printf '%s\n' '^tools/' '^TAOM.Tests/' '^Main/_Module/ModuleData/' '^docs/' '^\.claude/hooks/check-' '^\.claude/rules/moduledata-validation.md' '^\.gitattributes' '^Dependencies/Foundation/PatchShield.cs')` prints nothing.

---

## Disposition table

| 1.5.x commit | Subject | Group | Disposition | Step |
|---|---|---|---|---|
| `d28d9a7d3` | every culture names its executioner | culture-xml-data-gates | Adapted: only `CultureXmlCommentPlacementTests`. Skipped: `executioner` data (v1.5.x field; not in v1.4.8 XSD), `CultureExecutionerTests`, `NotableTemplateGenderTests` (v1.5.0 Trader start), two doc lines | A1 |
| `bc5b5d71d` | recruitment perks per v1.5.2, dead faction data | xslt-template-coverage-gate | Adapted: the gate and three dead templates with their rows. Skipped: perk routing, 44 faction strings, 16 `module_strings.xslt` templates (v1.4.8 reads them), docs | A2 |
| `fa8c06f82` | /deep-review senior lenses, XSD gate | issue-621 | Transplanted: tool, tests, pass, hook code, rule rows. Lens half and agents: WP138 | B1 |
| `1ebcf86a8` | item registry reads only Items documents | validator-item-registry-items-root | Adapted: `_scan` rule and a rewritten test (K1). Skipped: armour fixtures and checks (04da4a0d0) | B2 |
| `d96167efa` | Edoras siege in the battle picker | custom-battle-scene-gate | Adapted: the test (at `babcb8335`). Skipped: the Edoras row | A3 |
| `babcb8335` | Codex review fixes for the KEYforce art | custom-battle-scene-gate | Adapted: test end state. Skipped: `generate_enlistment_rosters.py` and test (1d8fc1d8e's filter), RCA, REVIEW-LOG, track record, prompt. Lesson and `.ai` entries: hand-off | A3 |
| `e166d8c84` | cap evil party-size feats at 10% | faction-card-feat-gate | Adapted: the gate, Mordor's card at +20%. Skipped: every feat value, garrison, string retune and doc | A4 |
| `6553b8c70` | RCA addendum for the party-size cap | faction-card-feat-gate | Lesson only, reworded: hand-off to WP142. Skipped: RCA addendum, `gamemodels-services.md` line | Maintenance |
| `ca92db878` | every Faction reference must name a clan | issue-949 | Adapted: all named parts | B3 |
| `2225939cd` | review record for the Dale follow-ups | issue-949 | Skipped here: RCA and REVIEW-LOG (bookkeeping; RCA mixes face-variety work absent here), `HeroFaceAdapter` comment and face docs. Two lessons: hand-off to WP142 | Maintenance |
| `487e3d9a9` | seven clan banners lose blank and debug layers | issue-951 | Ported by pick; two feature-doc hunks dropped | A5 |
| `48d66b59a` | schema check names language files and stylesheets | tools-validate-xml-schemas | Tool and tests in the transplant, rerun hint in the pass, PatchShield comment and pin ported. Lens: WP138. Line-ending doc claims: skipped (1.5.x measurements; re-measure in WP142 if wanted) | B1, E2 |
| `f963c6188` | one guard for generated XML blocks | tools-generator-marker-region | Adapted; Arthedain hunks skipped | C1 |
| `c73cf2961` | culture generators keep blocks, never half-write | tools-generator-marker-region | Adapted; `test_generator_regions_in_sync.py` skipped | C2 |
| `b409e6505` | sweep follow-ups, stale and wrong docs corrected | tools-generator-marker-region | The duplicate-id doc sentence taken in B1. README bullets and #974 lesson: hand-off to WP142. Skipped: `adapters.md` (bullet absent here), recipe and faction docs (Arthedain), RCA | B1, Maintenance |
| `4644bf493` | pin the engine check for repeated clan ids | tools-xsd-schema-gate | Ported inside the test transplant | B1 |
| `c1ce78207` | schema check names project.mbproj loads | tools-xsd-schema-gate | Transplanted; rule and modding-doc lines taken. Lens: WP138. Skipped: 1.5.x-only RCA edits | B1 |
| `bc30b76da` | mbproj audit by type, RCA follow-up gate | issue-983 | Adapted: audit (managed half verified, native UNVERIFIED), lint, hook. Skipped: run-protocol, snapshot copy (re-snapshot from install), modding docs pending native check | D3, D4 |
| `2420508fb` | refuse items written to a slot they cannot fill | issue-729 | Adapted by block | B4 |
| `8caac7da5` | gates for entry points, services and sprites | tests-architecture-gates | Adapted: one assembly, `Main` only, baselines regenerated. ADR text: 1.4.x equivalent by the maintainer. Skipped: `NotableTemplateGenderTests` hunk, rules, release skill (WP143) | E1, E3 |
| `f1675c058` | validator codes and one command for data gates | tools-data-gates-validator-codes | Adapted: three codes (no ammo half), `data_gates.py`, `--strict`, `--check`, `--module`. Skipped: `fords_of_war`, `generate_career_kits.py`, `audit_armory_refs.py`, `audit_fbx_lods.py`, codes-doc move (WP138), `/localize` Case E (WP138) | B5, B6, B7 |
| `569f6750d` (K2, shared) | Uruk-hai skirt worn as body armour | issue-729-uruk-skirt | Comments, rule row, RCA with no hill-troll wording; data already on 1.4.x; test-file comment of the memory audit taken by WP117 | B6 |
| `709649c3f` (shared hunk) | tool hunk adds the attribute allowlist | issue-646 | Skipped by D15; its pre-release item is WP115's and lens hunk WP143's | B1 |
| `09f3126a7` (shared tool hunk) | exact mbproj ids | issue-987 | Tool's small hunks in the transplant; audit half adapted to v1.4.8 (14-id table; native half UNVERIFIED), or skipped with the reason in the hand-off if it does not port cleanly; modding docs WP144; lint and hook half WP138 | D3b |
| `332f2ac71` (shared hunk) | TAOM_Map pin | collision-body-gate | Its test adapted to the 1.4.x map's eight ids (WP108's hand-off) | B1 |
| XML-DATA-02 (validator half) | header prints module vintage | finding | New work | B8 |
| XML-DATA-05 | prefab budget hardcodes the desktop, counts one module | finding | New work | D1 |
| XML-DATA-07 | no XSD gate on 1.4.x | finding | Ported. Its "allowlist `taom_body_length`" direction is overridden by D15 | B1 |
| XML-DATA-09 | upsert can corrupt a file | finding | Ported | C1, C2 |
| XML-DATA-10 | asset warnings, false scene suspects | finding | New work plus a doc sentence. `soln_voice_definitions` stays a known warning | D2 |

Prerequisites from other WPs, not ported here: `bd457ee6e` (WP133), `3dac7ab5b` (WP117), `5ee49d05f` (WP107), `ef6afeb67`, `b23a27ef4`, `719074072` (WP108). Not this WP: `12afef4d2`, `925db38d1`, `04da4a0d0` (SKIP-FEATURE). The 1.5.x commits `5f87810ed`, `334c187e4`, `ff2114556`, `002f8ff7e` are newer than the classified manifest: not ported; see Maintenance.

## Test plan

| Gate | File | Starts | RED proof |
|---|---|---|---|
| Comment placement | `TAOM.Tests/Core/CultureXmlCommentPlacementTests.cs` | green | A1 throwaway comment |
| XSLT coverage | `TAOM.Tests/Migration/XsltTemplateCoverageTests.cs` (`BindingVerification`) | red, 3 rows | A2, real |
| Scene levels | `TAOM.Tests/Features/CustomBattles/CustomBattleSceneLiveDataTests.cs` (3) | green | A3 throwaway on `DeclaredLevels` |
| Card vs feat | `FactionMapDataTests` 12 rows | red, 1 row | A4, real |
| Banner keys | `tools/tests/test_banner_icon_ids.py` | red, 7 keys | A5, real |
| XSD | `tools/tests/test_validate_xml_schemas.py`, `SchemaInvalidPassTests` | green | B1, removed clan attribute |
| Registry root | `test_item_registry_counts_only_items_documents` | red | B2 |
| Faction refs, exit 3 | `ValidatorContractTests`, `BuildRegistriesTests`, `EntryPointCrashTests`, `test_taom_query`, `test_hooks.sh` | red | B3 |
| Slots | `ItemSlotRejectedTests` | red | B4 |
| Troop ids, townsfolk, weapons | `TroopIdRemovedTests`, `TownsfolkBattleRosterTests`, `WeaponSlotCoverageTests` | red | B5 |
| Data gates | `tools/tests/test_data_gates.py` | red | B7 |
| Vintage | two tests in `ExtraRefRootTests` | red | B8 |
| Marker region | `test_marker_region.py`, `test_region_writers_in_place.py` | red | C1, C2 |
| Prefab budget, scene audit | `test_check_prefab_budget.py`, `test_audit_scene_names.py` | red | D1, D2 |
| Mbproj | `test_audit_mbproj_registration.py` (36; 86 after D3b) | red | D3, D3b |
| FOLLOW-UP | `RcaFollowUpScannerTests`, `RcaFollowUpGateTests`, hook case | red | D4 |
| Architecture | four new test classes, one new `BundledDependencyManifestTests` method | green against regenerated baselines | E1 throwaways |
| Finalizer value | `PatchShieldFinalizerTests` | green | E2, a behaviour pin |

Structurally untestable: nothing runs the game. `NATIVE_BY_TYPE` and the five native ids on v1.4.8 stay UNVERIFIED. Model tests on the neighbouring class in each file; never add a production dependency for a test.

## Done criteria (whole plan)

- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=`: 0 failed.
- [ ] `python -m unittest discover -s tools/tests -t .`: no new failure vs A0; `bash tools/test_hooks.sh`: no new failure.
- [ ] `python tools/validate_xml_schemas.py`: `PASS`, 0 failed. `python tools/validate_moduledata.py`: no ERROR beyond the WP101 baseline.
- [ ] `python tools/data_gates.py --list` exits 0; `ls tools/validate_xml_schemas.py tools/data_gates.py tools/_marker_region.py tools/shipped_troop_ids.txt` lists all four.
- [ ] `git grep -c -e 'Gate per file kind' -- .claude/rules/moduledata-validation.md` is `1`; `git grep -c '_parse_like_engine' -- tools/validate_xml_schemas.py` at least `1` (WP138 K4, K5).
- [ ] `git diff --stat bannerlord-1.4.5...HEAD -- CHANGELOG.md CLAUDE.md AGENTS.md tools/README.md docs/INDEX.md docs/reference/feature-map.md docs/reviews/lessons docs/reviews/REVIEW-LOG.md Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj docs/adrs` prints nothing at the executor's hand-off (after Step E3, only the two ADRs appear under `docs/adrs`).
- [ ] `git log --format=%s bannerlord-1.4.5..HEAD | grep -vc 'v2.0.28 - '` prints `0`; `git log --format=%B bannerlord-1.4.5..HEAD | grep -cE 'Co-Authored-By|Generated with'` prints `0`.
- [ ] Hand-off states: test totals, the validator per-code counts, `--live` result, `apply_culture_skills_traits --check` result, `INCONSISTENT_WEAPON_SLOT` count, both baseline row counts, `data_gates.py --all` table, D3b's outcome (commit, or skip with reason), Step E3 owed to the maintainer.

## STOP conditions

- A0: any prerequisite missing, or (for A2, A4, A5, and the snapshot in D3) the laptop Armory is still v2.0.26 (D20).
- A gate fails on a row the plan does not name: a fourth dead XSLT template, an eighth banner key, a card other than Mordor's, a real `ITEM_SLOT_REJECTED`, `TROOP_ID_REMOVED` or `BROKEN_FACTION_REF` row. Report the row. D13 needs verified, non-OUT data: do not fix or allowlist it.
- Any impulse to allowlist `taom_body_length` or `family_type`. D15 forbids the first; the second is the old Armory.
- A step would change a feat value, roster, troop, kingdom, culture or a TaleWorlds-facing runtime path.
- `validate_moduledata.py` passes 35 s on the laptop (the hook kills at 45 s, and a kill reads as a pass).
- `regenerate` writes fewer than 857 ids.
- `promote_borrowed_cultures.py` has no `pending_writes` list; the hook no longer has its `--code` list or `hookSpecificOutput`; a `git checkout <sha> <path>` precheck shows a diff the plan said is empty.
- A pinned 1.5.x blob imports a module absent on this tree beyond those named.
- A step's verification fails twice after a reasonable fix attempt, or a commit hook blocks for a reason you cannot fix without `--no-verify`.
- Engine behaviour differs from "Engine facts": report it; do not re-derive and improvise.
- `lxml` missing, `BANNERLORD_GAME_DIR` unset: environment; report, do not install.

## In-game checks owed (for the maintainer)

1. **Mordor's card**: character creation, culture stage, and the faction screen: Mordor shows +20% party size limit in English and one other language. Log: none.
2. **Seven banners**: in a campaign, encyclopedia clan pages for Nos Alenwe, Nos Iminye (Mirkwood, kingdom banner), Zorian (Rhun banner), the Rivendell and Lothlorien houses (`clan_rivendell_*`, `clan_lothlorien_*` from the diff; no blank top layer) and Torabai and Khorvash (Khand; no garish debug colour). Log: `rgl_log_<pid>.txt` shows no new banner line.
3. **Three hero templates removed**: start a campaign: no new "Null object reference" lines in `rgl_log_errors_<pid>.txt` compared with a build before this WP. Nothing else should change, since the templates never matched.
4. **`--live` on the public Armory**: `python tools/validate_xml_schemas.py --live` after WP101's refresh; expected: `taom_body_length` rows on `lotr_monster_elk.xml` and `lotr_monster_animalia.xml` (matching E1/E2), no `family_type` rows. UNVERIFIED until run; a different result is worth a look.
5. **Skill-set drift**: decide `taom_canonical_lord_I1_0_skills`: generator or file wins. Do not `--apply` before deciding.

Maintainer-only: no `settings.json` edit is needed. The ADR text `8caac7da5` changes (`docs/adrs/002-thin-entry-points.md`, `007-adapter-pattern.md`: "what holds them") is the maintainer's Step E3, with its exact edit; WP143's amendment step reads the gates E1 landed.

## Maintenance notes

- **Hand-offs to WP142** (lessons and README, reworded for 1.4.x): `docs/reviews/lessons/data-content-cultures.md` "Gated since" (`6553b8c70`, cite `4f72e160e`, Mordor only); `testing-qa.md` "A data gate checks what the engine asks for" (`babcb8335`), "A gate counts what it parsed against what exists" (`2225939cd`); `build-tooling-workflow.md` "Give a scratch transform a timeout" (`2225939cd`), "Before you add a gate, prove no gate already catches the case" (`b409e6505`), the marker-region Prevent sentence (`b409e6505`); `xslt-moduledata.md` stylesheet-coverage lesson (`bc5b5d71d`), `fa8c06f82` and `c1ce78207`'s lessons; `tools/README.md` rows for `validate_xml_schemas.py`, `data_gates.py`, `_marker_region.py`, the two generator bullets (`b409e6505`), `check_prefab_budget.py`, `audit_mount_parity.py --strict`; any `translate_with_claude.py --lang` doc hit B6 listed.
- **Hand-offs to WP143**: `.ai/review-reference.md` two entries from `babcb8335`; `.githooks/pre-commit` `--code` list must gain this WP's four codes; `/release` must reseed `tools/shipped_troop_ids.txt` (`python tools/validate_moduledata.py --regenerate-shipped-troop-ids <tag>`, first at v2.4.0, D8), and until then the baseline header names the branch; `DependenciesPairing_...` in the release skill.
- **Hand-off to WP138**: K1, K2, K3, K4, K5 and K12 are true after this plan. C8's codes doc should bring `test_every_emitted_code_has_a_row_in_the_codes_doc` and the `CODES_DOC` row back. The lens files come from there.
- **Hand-off to WP140**: `DeclaredBrushes()` keeps its name; `ModuleFolders` and `GameDir` are internal; its Step C6 adds its nine gate names to `data_gates.py`'s `gui-tests` row, which it requires to exist exactly once.
- **Owed, no WP takes it**: the "native reads a row's type" modding docs, and a v1.4.8 native re-check of D3b's five native ids, `soln_animations`, `soln_sound_event_data` and `NATIVE_BY_TYPE` with WP137's tools. WP144 records both as deferred. If D3b recorded a skip, its table stays out until that check.
- **Deferred, on purpose**: `INCONSISTENT_WEAPON_SLOT`'s ammo half (needs a small item-type registry; add it when another gate wants `Type=`). 1.5.x's `5f87810ed` ("validator reuses its file reads and ref walk") and `334c187e4`, `ff2114556`, `002f8ff7e` post-date the manifest: unclassified; a later sync should diff them. WP115's note about a `Settlement.` ref sweep belongs in a future code, not added here (YAGNI until `334c187e4` is classified).
- **Interactions**: `CommitGateCoverageTests` and `test_every_issue_pass_is_reached_from_main` will fail any future ERROR code or pass not wired. `data_gates.py` must lose a row when a tool is deleted (`test_every_python_gate_names_a_script_that_exists`). The two C# baselines only shrink: a refactor that shortens a listed class must lower its number.
- **Review focus** (`/deep-review` applies: more than 2 C# files; recommend it, and `/verify`, to the integrator; `/security-scan` after the hook edits): the hook still fails open on rc 2, 3 and 124; `SCHEMA_INVALID` skip paths are one warning; B1 removed every allowlist trace; the `main()` sort moved out of the `if`; `_scan` drops no real definition on the public Armory; `data_gates.py` rows match the live tools; `check-doc-config-drift.sh` `--staged` reads the index.
- **Line endings**: worktree files are CRLF over LF blobs; language files are `-text` with CR CR LF rows: A2 and A4 edit them as bytes. Keep it that way.
