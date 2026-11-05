# Plan 141: Make the 1.4.x data and art tools safe to run: no silent paid calls, no live writes by default, no half-applied files

> **Executor instructions**: Follow this plan step by step, in your own worktree (see "Git workflow").
> Run every verification command and confirm the expected result before moving on. If anything in
> "STOP conditions" occurs, stop and report; do not improvise. The plan has four Parts, each a clean
> hand-off point with its own done criteria: Part A (tool safety fixes, P2), Part B (map tool hardening,
> P2), Part C (the RDC gate and tpac doc corrections, P3), Part D (the graphify wrapper, P3, optional:
> leave it for a later session if A to C used this one). Do not edit `plans/README.md`: the integrator
> owns it during this programme. Put the final counts in your hand-off.
>
> **Never run a tool in this plan against real data**, before or after its fix, except the two read-only
> commands a step names (A4 dry run, A5 data validator). Never pass `--apply`. Several of these tools
> write the live game install or the repo's data on an ordinary run (see "Safety rules").
>
> **Drift check (run first, in the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- tools/translate_with_claude.py tools/generate_starter_kit.py tools/generate_ranged_ladder_items.py tools/oneoff/generate_tavern_mercenaries.py tools/generate_enlistment_rosters.py tools/check_generator_item_refs.py tools/fix_upgrade_armour_regressions.py tools/rebalance_troops.py tools/generate_clan_heraldry.py tools/oneoff/convert_tripo_prop_textures.py tools/add_map_villages.py tools/Apply-MapVillageNames.py tools/tests TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs docs/features/starting-equipment-tuning.md docs/features/ranged-ladders.md docs/features/tavern-mercenaries.md docs/reference/taom-map-settlement-naming.md docs/modding/settlements.md docs/modding/module-map.md docs/reference/ue-to-bannerlord-asset-pipeline.md docs/reference/lotrlome-warg-changes.md docs/ai-includes/creature-mount-authoring.md docs/tools/spider-skeleton-tpac-tools.md docs/reference/mcp-servers.md docs/reference/provenance-register.md tools/test_hooks.sh .gitignore`
> Earlier WPs are expected to have changed some of these (WP135 the translator and perhaps the map
> tools, WP103/WP116 `tools/test_hooks.sh`, WP142's sibling docs). Compare each live file with the
> excerpts in "Current state" before you edit it and STOP only on a real mismatch in the lines a step
> touches. Each step names the overlap it expects.

## Status

- **Priority**: P2 (Parts A and B); P3 (Parts C and D).
- **Effort**: L (up to 15 commits; plan on two executor sessions: A and B, then C and D).
- **Risk**: MED. The code risk is low: Python tools, their unit tests, one C# test message and docs, no
  game code, no ModuleData. The operational risk is that several tools touched here write the live
  game install or repo data on a bare run (the 1.4.x map renamer even at import), so one careless
  invocation during the port damages the laptop's install. The safety rules and the install snapshot
  in Step 0 contain that.
- **Depends on**: `plans/137-dev-tools-native-and-src.md` (merge order only: no file here depends on
  WP137's content), and through it WP103 and the wave-0 harness (WP102 deleted the CHANGELOG gates,
  WP103 anchored the hook registrations and widened the git-gate matcher to `Bash|PowerShell`).
- **Category**: dx (developer and data tooling) with data-integrity gates.
- **Wave / lane**: wave 5, lane F.
- **Executor**: laptop.
- **Decisions applied**: D1 (no `CHANGELOG.md` staging; the commit body is the entry), D7 (no co-op or
  dedicated-server text: 72cad16b1's `DsAssetPackages` sentences are dropped), D8 (subjects carry
  `v2.0.28`), D10, D19 (git through the Bash tool only), D20 (no ModuleData edits; no tool run against
  the laptop's data except the read-only validator).
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. During planning `origin/bannerlord-1.5.x` moved to `31292c035`; three of its
  71 newer commits touch tools in this plan (`ad8f13d00`, `44d4dba46`, `c30c4e618`). They are outside
  this programme's classification and are not ported here (Maintenance notes).
- **Issue**: port of #616, #617, #677 and #995, #798, #804, #812 and #813, #930, #1039, #1041, #1042,
  #1043 and #763. New work (the renamer test file, the RDC gate test, the civilian-set filter in its
  1.4.x form, the enlistment fallback-table spec): orchestrator creates the issue before
  implementation lands.

## Why this matters

The 1.4.x copies of these tools carry defects the 1.5.x line already fixed, and most of them fail
silently or cost money. `tools/translate_with_claude.py` reads `--max-entries 0` as "no cap" on paid
API calls (`origin/bannerlord-1.4.5` line 930: `if args.max_entries and ...`). `tools/Apply-MapVillageNames.py`
runs at import with no dry run and rewrites all 438 of its ids in all 12 language files of the live
`TAOM_Map` on every run, resetting those names' translations to English, and evicts nothing from
`tools/translation_cache/<lang>.json` (996 settlement-name rows in each of the DE and RU caches,
counted this session), so the next translator run writes old names back. `tools/oneoff/generate_tavern_mercenaries.py`
has no dry run (any run writes whatever it plans) and names `troops_mistymountainorcs.xml`, a file
`f577bd547` removed on this line, so every run crashes part-way. `generate_enlistment_rosters.py --seed-missing`
writes an XML comment containing `--`, so it can never succeed. The starter kit generator can delete
another author's rows and half-apply a refused run; it and the ranged ladder generator default to
writing the `lotraom-assets` mirror the maintainer syncs by hand; the Tripo texture converter defaults
its output into the live `TAOM_Map` textures and overwrites without backup. After this plan every one
of those tools is dry by default or atomic, refuses rather than half-writes, and has unit tests that
pin the behaviour. Parts C and D add a read-only gate for packages the client silently skips and the
graphify code-graph wrapper the 1.5.x workflow uses.

## Current state

All excerpts are `origin/bannerlord-1.4.5` at `1b00d1acd`. Every Python file below is stored with LF
and checked out with CRLF on this laptop (`core.autocrlf=true`, `git ls-files --eol` shows `i/lf w/crlf`),
which is why this plan applies patches with `git apply -3` (index based) rather than plain `git apply`.

**Translator (Part A1).** `tools/translate_with_claude.py`: `def main():` at 841; line 848
`p.add_argument("--max-entries", type=int, default=None, help="Cap API entries for testing")`;
the `--sync-ids` argument at 849-851; `args = p.parse_args()` at 864; the dry-run/apply check at 866-867;
the sync block 885-911, ending with the `Ids seeded` print at 911; the cap at 930-932:
```
930    if args.max_entries and len(need_llm) > args.max_entries:
931        print(f"  --max-entries cap applied: {args.max_entries}/{len(need_llm)} LLM entries")
932        need_llm = need_llm[:args.max_entries]
```
`call_model` (646), `call_claude_batched` (654), `write_back` (779), `load_overrides` (366),
`load_cache` (374), `save_cache` (382), `english_source_files` (234), `discover_entries` (264),
`TAOM_LANG_DIR` (63) and `game_modules_root` (66) all exist, so the 1.5.x test can patch them.
`TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs:157-162` ends its assert message with
`"\n\nFix: python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply");`
(identical to `a9f2d7eda^`).

**Starter kit (A2, A3).** `tools/generate_starter_kit.py` differs from `a9f2d7eda^` only by the 1.5.x
`RETIRED_DONORS`/#629 lines, the `ASSET_REPO` import and one `_refuse_shrink` message, so the 1.5.x
guard patches apply. Line 14 `WHAT IT WRITES (live Armory and, when present, the lotraom-assets mirror)`;
46 `# write to the Armory (+ asset repo)`; 72 `DEFAULT_ASSET_REPO = Path(r"E:\repos\lotraom-assets") / "v1.4" / "LOTRLOME_Armory"`;
`block_re` 425, `apply_xslt` 488, `revert_xslt` 550, `apply_pieces` 574, `revert_pieces` 590,
`_ID_RE` 888, `apply_plan` 906, `verify_plan` 969, `revert_plan` 1004, `main` 1079 with
`--asset-repo default=str(DEFAULT_ASSET_REPO)` and `--no-asset-repo` at 1085-1086, the missing-Armory
`SKIP ... return 0` at 1098-1099, and the mirror target at 1110-1114:
```
1110    targets = [Path(sources.armory) / "ModuleData"]
1111    if not args.no_asset_repo and Path(args.asset_repo).exists():
1112        targets.append(Path(args.asset_repo) / "ModuleData")
```
`tools/tests/test_generate_starter_kit.py` imports no `mock`; it has `WD_XSLT` (126), `TestStylesheets`
(321), `TestPiecesFile` (378), `_plan_in` (443). `tools/generate_ranged_ladder_items.py`: docstring lines
12 and 38, `DEFAULT_ASSET_REPO` (v1.4 path) at 58, `--asset-repo default=str(DEFAULT_ASSET_REPO)` at
300-301, and the mirror tree added whenever the folder exists at 316-321. Its `GeneratorTests`
(`tools/tests/test_ranged_ladder.py:647`) always passes `--asset-repo` explicitly in `_run`. There is no
`_gamedir.armory_trees` on this line (`tools/_gamedir.py` defines only `game_dir`, `game_modules`,
`ensure_exists`). The mirror rule: "The `E:\repos\lotraom-assets` mirror is Mike's to sync, never a
session's (2026-09-22)" (1.5.x `.claude/skills/armory-audit/SKILL.md:59`); it names the whole
`lotraom-assets` folder, so it covers the `v1.4` mirror too.

**Tavern mercenaries (A4).** `tools/oneoff/generate_tavern_mercenaries.py` (203 lines): no argparse; the
module writes on any run (`write(path, ...)` at 140 inside `append_copies`, `write(SPCULTURES, xml)` at
180). Lines 61-63 name `("mistymountainorcs_fighter", "mistymountainorcs", ...)`; no
`troops_mistymountainorcs.xml` exists (`git ls-tree`), the `mistymountainorcs` culture's tavern binds
`NPCCharacter.goblin_fighter_merc` (`Main/_Module/ModuleData/taom_spcultures.xml:4918`, culture opened
at 4592) and `f577bd547` (an ancestor of this line) removed that troop file. Line 149
`for match in list(culture_pattern.finditer(xml)):` edits `xml` while iterating offsets taken before the
edit. All 14 picked cultures already bind exactly the `_merc` copies the corrected `PICKS` names, and all
20 `_merc` copies exist once each (checked this session), so a dry run after the fix should change
nothing. The 1.4.x Harad picks stay `harad_noble` (the 1.5.x `harad_redserp_noble` and `harad_pz_levy`
do not exist here). `docs/features/tavern-mercenaries.md:125` still lists `mistymountainorcs_fighter`;
line 183 is the usage step.

**Enlistment generator (A5).** `tools/generate_enlistment_rosters.py:936`
`insert = ('\n    <!-- seeded by generate_enlistment_rosters.py --seed-missing -->\n'` puts `--` inside
a comment; `parse_written` (753) then refuses the output. `RANKS` at 89; `DEFAULT_ROSTER_ITEMS` at 167 is a
dict of `(assignment, rank)` keys to lists of `(slot, item id)` tuples (24 distinct ids; all 24 resolved
at planning against this laptop's then Armory `v2.0.26`, which WP101 replaces with the public build, so
A5 re-checks them). There is no
`tools/tests/test_generate_enlistment_rosters.py` on this line, and no `ISSUE_LAUNCHER`.
`tools/check_generator_item_refs.py:82-90` `GENERATORS` ends with the `generate_starter_armor.py` spec
(89); `refs_from_tables` (111) walks dict values only and skips the first `tuple_from` tuple columns;
`ID_SHAPE_RE` (69) needs a lowercase `word_word` shape. `tools/tests/test_check_generator_item_refs.py`
imports the module as `cg` and has `LiveInstallGateTests.test_no_generator_writes_a_retired_id`, which
asserts no registered generator writes an id the installed modules lack. That test and
`ValidatorWiringTests.test_missing_id_becomes_a_warning_under_the_code` were ALREADY red at planning, on
`1b00d1acd` against Armory `v2.0.26`, for two other generators (`generate_gondor_troops.py` 5 ids,
`generate_char_creation_equipment.py` 1 id). They may stay red after WP101; Step 0's list records it.

**Troop tools (A6).** `tools/fix_upgrade_armour_regressions.py` and its test are identical to
`8465cc984^`; `write_changes` (466) parses and writes each file inside one loop, so a bad file late in
the batch leaves earlier files written. `tools/rebalance_troops.py:402-404`:
```
402 MILITIA_BINDING_RE = re.compile(
403     r'(?<![A-Za-z0-9_])(?:melee_|ranged_)?(elite_)?militia_troop"?\s*(?:=\s*"|>)\s*'
404     r'NPCCharacter\.([A-Za-z0-9_]+)')
```
while the validator's copy (`tools/taom_schema.py:1081`) takes either quote. `troop_weapon_classes`
(287-301) walks every `.//equipment`, civilian sets included, and its docstring wrongly says civilian
sets hold no inline weapons: `imladris_recruit` carries its only bow (`Item.highelf_longbowd`) in an
`<EquipmentRoster civilian="true">` (`troops_rivendell.xml`). Callers: the Bow/Crossbow swap, Throwing
parity and the Polearm/TwoHanded swap. Latent today (K2: no 1.4.x troop's swap flips), P3.

**Clan heraldry (A7).** `tools/generate_clan_heraldry.py` and `tools/tests/test_clan_heraldry_specs.py`
are byte-identical to `b75fbab07^` (`git diff` empty). The targets (`spclans.xslt`, `characters/clans.xml`,
`taom_partyTemplates.xml`) are `w/crlf` here, so in a fresh worktree the spec suite's no-op test may
already fail in the Step 0 baseline; A7 fixes that.

**Tripo converter (A8).** `tools/oneoff/convert_tripo_prop_textures.py:38-40` defines `DEFAULT_SRC` and
`DEFAULT_DST` (the latter inside `Modules\TAOM_Map\AssetSources\Scenes\Mordor\textures`), used as the
`--src`/`--dst` defaults at 78-79; outputs overwrite silently; the AO regex is the old `\bao\b`. The
1.5.x end state (`041808e5f`, last change on 1.5.x through `e45f6df2c`) needs only numpy and Pillow, both
installed here (numpy 2.5.3, Pillow 12.3.0). `docs/reference/ue-to-bannerlord-asset-pipeline.md:89-92`
tells the reader to re-run it "with `--src <export dir>`".

**Map tools (Part B).** `tools/Apply-MapVillageNames.py` (557 lines): `import os, re` (40), the duplicate
check runs at module level (519-526), `patch(path, mode)` (535) rewrites every `NAMES` id and writes the
file (548), and lines 552-557 call it for the master and all 12 languages at import. Its `NAMES` dict
(438 ids) equals the 1.5.x end state's (`af2b198e4`, compared with `ast.literal_eval` this session).
`tools/add_map_villages.py` (371 lines): imports end at 43 (`from _gamedir import game_dir`), `LANGS` at
51, `settlement_position` 171-173, `insert_after_settlement` 226-232, `loc_row` 235, `check()` 257-272
(starts `findings = []`), `main()` 292 with the `--check` OK message at 318 and the planning block at
328-338 (`nl = detect_newline(master)` then `blocks, placeholders, per_parent = {}, 0, {}`).
`tools/tests/test_add_map_villages.py`: fixtures `MASTER`, `LOC`, `SCENE`, `ROW`, `PRESENT` equal the
1.5.x ones; `CheckTests` 244-276, `BatchGeneralisationTests` at 277. There is no `tools/name_tags.py` on
this line unless WP135 ported it (check in B2). The laptop's installed `TAOM_Map/ModuleData/settlements.xml`
(dated 2026-09-07, 988 settlements) holds none of the 15 `VILLAGES` ids, so `add_map_villages.py --check`
fails there on missing rows; never use it as a pass criterion here (WP115 records the pairing fact).
The same file carries a content defect this plan does not fix: `town_EN1` is `Galtrev` in
`settlements.xml` but `Epicrotea` in the BR, DE, FR, IT, SP and TR `loc_settlements.xml` (DE line 43).

**tpac docs (Part C).** `docs/reference/lotrlome-warg-changes.md` (identical to `426f0dc88^`) says at 529
that the item checksum's "algorithm is not known"; section 13 (582-601) already records that bytes 28-35
of a tpac header are the TOC size. `tools/tpac_skeleton_extract.py` writes
`struct.pack('<II', MAGIC, 2) + item_guid + struct.pack('<III', 1, 0, 0)`: the skeleton's item GUID as the
package GUID and zeros in bytes 28-35. `docs/ai-includes/creature-mount-authoring.md:151` (row 7) says
the material binding "cannot be patched from files"; item 17 (458-466) calls a standalone skeleton tpac
"an unproven structure". `docs/tools/spider-skeleton-tpac-tools.md:33` gives the old crash reason.
`docs/investigations/native-commit-audit-2026-08.md:146` ("the shipping client READS RuntimeDataCache")
and `:259` ("the EDITOR requires RDC") settle the RDC premise on this line. This laptop's Armory and
TAOM_Map are cooked (`AssetPackages/pack*.tpac`, no `RuntimeDataCache` folder), so `check_rdc_entries.py`
is a desktop (loose `Assets/`) tool.

**graphify (Part D).** Not installed on this laptop (`which graphify` empty, no `graphify` module) and
there is no `E:` drive; the 1.5.x wrapper exits 3 for a missing environment and its default output root
is `E:\graphify\TAOM`. Its tests are hermetic (temp git repos) and need only git. The gate script sources
`.claude/hooks/_pybin.sh` and calls `taom_pybin_degraded`, which exists here (`_pybin.sh:131`). Hooks
on this line are mode `100644` (28 of 29). `.gitignore:143-148` already ignores `graphify-out/`.

**Engine facts used (verified this session).** Installed-DLL decompiles from
`pwsh -NoProfile -File tools/taom-src.ps1 path <Type>` (cache `C:\Users\mikew\.taom-src\v1.4.8\`):
- `TaleWorlds.Core.BasicCharacterObject.Deserialize`: `<equipment>` children of `<Equipments>` are
  collected (365) and applied to every set through `_equipmentRoster.AddOverriddenEquipments` (412);
  `<EquipmentRoster>` children go to `MBEquipmentRoster.Init` (372); `<EquipmentSet>` references read
  `equipmentType` (388-390) or the legacy `civilian` (395-398).
- `TaleWorlds.Core.MBEquipmentRoster.InitEquipment` (88): `equipmentType` (92-94), else
  `civilian="true"` gives `EquipmentType.Civilian` (99-101).
- `TaleWorlds.Localization.MBTextManager.ChangeLanguage` (61) is where `LocalizedTextManager.LoadLanguage`
  runs (68); the language comes from `BannerlordConfig` (dump `BannerlordConfig.cs:219-223, :584`) and
  module strings load at startup (`Module.cs:294-302`). A save load reloads neither, so a translated
  name changes only at the next game start or a language switch.
- `Campaign.cs:1432-1438` (dump) unregisters non-ready objects on load (context for Maintenance notes).

**Overlaps with earlier WPs (check before the step that touches each file).**
- WP135 (wave 4) step A7 lands this plan's translator hunks itself: the cap fix, the negative-cap refusal,
  `--seed-only` (with two extra guards from `b11b6d142`), `tools/tests/test_translate_caps_and_seed.py` and
  the `LanguageFileCoverageTests` message, and tells this plan to skip them. So A1's expected case is
  Case L (nothing to do, no commit). WP135's optional Part D may add `tools/name_tags.py`, without
  touching the map writers; B2 branches on whether it exists and may keep `carry_tag` only then.
- WP136 leaves `generate_enlistment_rosters.py`'s test to this plan; WP114 skips the starter kit; no
  other pack touches the rest of Part A's tools.
- WP103 and WP116 rewrite `tools/test_hooks.sh`; WP142 to WP144 sync `tools/README.md`, lessons,
  `CLAUDE.md`, INDEX and feature-map after this WP (see "Hand-offs").

## Conventions that bind this plan

- TDD: each step adds or ports its failing test first, runs it red, then the fix, then green.
- Tool conventions (`tools/README.md` "XML I/O convention"): dry run by default, `--apply` writes, each
  file's BOM and line endings kept, every output parsed before any write, a non-`.xml` backup.
- ADR-002/003/004/005/007 bind C# only; the one C# edit here is a test message string.
- The MCM json2 rule (rename a persisted setting, never flip its default) is not touched.
- No em or en dash in anything you write (commit bodies, docs, comments you add).

## Commands you will need

| Purpose | Command (from the worktree root, Bash tool) | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~LanguageFileCoverageTests` | 0 failed |
| One tool test module | `python -m unittest tools.tests.<module>` | `OK` |
| Tool tests, all | `python -m unittest discover -s tools/tests -t . 2>&1 \| tail -3` | no failure the Step 0 list lacks |
| Tool failures list | `python -m unittest discover -s tools/tests -t . 2>&1 \| grep -aE "^(FAIL\|ERROR): [A-Za-z_.0-9]+ \(" \| sort` | compare with the Step 0 file |
| Hooks | `bash tools/test_hooks.sh 2>&1 \| tail -6` | no FAIL line the Step 0 run lacks |
| Docs | `python tools/lint_docs.py` | no new finding in a touched file |
| Data (A5 only) | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |

Both `-p:` flags on every build and test: `Main/TAOM.csproj:133-136` does not gate CopyModule on
`DisableModuleCopy`. Never `./build.ps1`. In Bash, drop the backslashes shown before `|` (markdown escapes).

## Scope

**In scope** (the only files you modify):
- Part A: `tools/translate_with_claude.py`, `tools/tests/test_translate_caps_and_seed.py` (new),
  `TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs` (the message only),
  `tools/generate_starter_kit.py`, `tools/tests/test_generate_starter_kit.py`,
  `tools/generate_ranged_ladder_items.py`, `tools/tests/test_ranged_ladder.py`,
  `tools/oneoff/generate_tavern_mercenaries.py`, `tools/tests/test_generate_tavern_mercenaries.py` (new),
  `tools/generate_enlistment_rosters.py`, `tools/tests/test_generate_enlistment_rosters.py` (new),
  `tools/check_generator_item_refs.py`, `tools/tests/test_check_generator_item_refs.py`,
  `tools/fix_upgrade_armour_regressions.py`, `tools/tests/test_fix_upgrade_armour_regressions.py`,
  `tools/rebalance_troops.py`, `tools/tests/test_militia_elite_bonus.py`, `tools/tests/test_rebalance_equipment.py`,
  `tools/generate_clan_heraldry.py`, `tools/tests/test_clan_heraldry_specs.py`,
  `tools/oneoff/convert_tripo_prop_textures.py`, `tools/tests/test_convert_tripo_prop_textures.py` (new),
  `docs/features/starting-equipment-tuning.md`, `docs/features/ranged-ladders.md`,
  `docs/features/tavern-mercenaries.md`, `docs/reference/ue-to-bannerlord-asset-pipeline.md`.
- Part B: `tools/add_map_villages.py`, `tools/tests/test_add_map_villages.py`, `tools/Apply-MapVillageNames.py`,
  `tools/tests/test_apply_map_village_names.py` (new), `docs/reference/taom-map-settlement-naming.md`,
  `docs/modding/settlements.md`, `docs/modding/module-map.md`.
- Part C: `tools/check_rdc_entries.py` (new), `tools/tests/test_check_rdc_entries.py` (new),
  `docs/reference/lotrlome-warg-changes.md`, `docs/ai-includes/creature-mount-authoring.md`,
  `docs/tools/spider-skeleton-tpac-tools.md`, `docs/reference/mcp-servers.md`.
- Part D: `tools/graphify_taom.py` (new), `tools/tests/test_graphify_taom.py` (new),
  `.claude/hooks/check-graphify-usage.sh` (new), `docs/features/graphify-code-graph.md` (new),
  `docs/reference/provenance-register.md`, `.gitignore` (the graphify comment), `tools/test_hooks.sh`
  (one appended section, plus rows in existing prefilter sections if they exist), and
  `.claude/settings.json` only as the maintainer's applied change in Step D2.

**Out of scope** (do not touch):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` (single-owner; nothing here needs them),
  `Directory.Build.props`, `docs/adrs/*`, any `Main/_Module/ModuleData/**` file.
- `CHANGELOG.md` (D1); `tools/README.md`, `docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`,
  `docs/INDEX.md`, `docs/reference/feature-map.md`, `CLAUDE.md`, `AGENTS.md`, `.agents/**`,
  `.claude/skills/**`, `.claude/agents/**`, `.claude/rules/**` and `.claude/hooks/session-start.sh`:
  WPs 142 and 143 sync them; "Hand-offs" lists what they owe.
- Every skipped commit's files (see "Disposition"): the cave troll pipeline, creature fit, LOD tools,
  the Painter/Blender map-icon scripts, `tpac_clone_metamesh.py`, `tpac_fix_item_checksums.py`,
  `register_armory_names.py`, `merge_live_file.py`, the `prep-tripo-prop` skill, one-off reset scripts.
- The game install and the `lotraom-assets` mirror: read them only as the steps say.

## Git workflow

- Worktree and branch (from the main checkout, Bash tool, D19):
  `git worktree add "C:/Users/mikew/source/repos/taom-port/141" -b port148/141-dev-tools-art-and-data bannerlord-1.4.5`.
  Work only there (`cd /c/Users/mikew/source/repos/taom-port/141 && ...` in each Bash call). Keep message
  files, patches and the maintainer script in `C:/Users/mikew/source/repos/taom-port/141-scratch/`
  (create it; outside the repo). Never push, open a PR, tag, or merge into `bannerlord-1.4.5`.
- Set `export MSYS_NO_PATHCONV=1` in any Bash call that runs `git show <ref>:.claude/...`.
- Ways to bring a 1.5.x change in (the step says which):
  1. `git cherry-pick -n <sha>` (never let cherry-pick, `--continue`, `-C`, `-c` or `--amend --no-edit`
     create the commit: `GIT_EDITOR=true` here commits the 1.5.x message).
  2. A path-limited patch: `git diff <sha>^ <sha> -- <path> > ../141-scratch/<name>.patch` then
     `git apply -3 ../141-scratch/<name>.patch` (index based, so the CRLF worktree does not matter).
  3. A whole-file end state: `git show <sha>:<path> > <path>` (writes LF; `git add` stores it correctly).
- To insert an extracted block before an anchor line (A2, B1), save this as
  `../141-scratch/insert_before.py` once and run
  `python ../141-scratch/insert_before.py <file> '<anchor regex>' <block file>`:
  ```python
  import re, sys
  path, anchor, block = sys.argv[1], sys.argv[2], open(sys.argv[3], encoding="utf-8").read()
  text = open(path, encoding="utf-8", newline="").read()
  m = re.search(anchor, text, re.M)
  assert m, "anchor not found: STOP"
  nl = "\r\n" if "\r\n" in text else "\n"
  block = block.replace("\r\n", "\n").replace("\n", nl)
  if not block.endswith(nl):
      block += nl
  open(path, "w", encoding="utf-8", newline="").write(text[:m.start()] + block + text[m.start():])
  ```
  Example: `git show 71fea53c8:tools/tests/test_generate_starter_kit.py | sed -n '/^class TestForeignRows/,/^class TestPiecesFile/p' | sed '$d' > ../141-scratch/a2-tests.txt`
  then `python ../141-scratch/insert_before.py tools/tests/test_generate_starter_kit.py '^class TestPiecesFile' ../141-scratch/a2-tests.txt`.
- Bookkeeping in a pick: a conflicted path you do not keep, `git checkout --ours <path>` then
  `git add <path>`; a cleanly changed one you do not keep, `git checkout HEAD <path>`. Never
  `git checkout --`, `git restore` or `git checkout .` (the confirm gate fires).
- Stage with explicit `git add <paths>`, then `git commit -F ../141-scratch/msg.txt` in the NEXT
  sequential Bash call. If `git status` still reports a cherry-pick after the commit, run
  `git cherry-pick --quit`.
- After every commit: the step's tool tests (and Build plus the Filtered test after A1). At the end of
  each Part: all tool tests, Build, full Test, Docs lint.
- Commit message (write it to `../141-scratch/msg.txt`):
  ```
  <type>(<scope>): v2.0.28 - <description, at most 72 characters in all>

  <Body for 1.4.x readers, wrapped at 72: what changed on the 1.4.8 line
  and why; for a partial port, what was left out and why. Name the issue
  ("Port of #1042"). No em or en dashes. No Co-Authored-By or
  "Generated with" line.>

  Research: <what was verified, for example the line read on this branch>
  Not-tested: <what needs a real run on the desktop>
  Ported-from: <40-hex sha>          <- one line per included 1.5.x commit
  ```
- `/deep-review` is not required (one C# test string changes); paid reviewers are not authorized.

## Safety rules for the tools in this plan

1. Never execute `tools/Apply-MapVillageNames.py` from a shell, before or after B2. Before B2 it rewrites
   13 live `TAOM_Map` files at import; after B2 only its tests run it, with `BANNERLORD_GAME_DIR` pointed
   at a temp tree.
2. Never execute `tools/oneoff/generate_tavern_mercenaries.py` before A4 is green (it writes the repo's
   troop files and `taom_spcultures.xml` on any run). After A4 the only run allowed is the dry run in A4.
3. Never execute `generate_starter_kit.py`, `generate_ranged_ladder_items.py`, `translate_with_claude.py`
   (paid API), `add_map_villages.py`, `convert_tripo_prop_textures.py`, `generate_clan_heraldry.py`,
   `fix_upgrade_armour_regressions.py` or `rebalance_troops.py` against real paths; their unit tests are
   the verification.
4. Step 0 snapshots four live files; the Done criteria require them unchanged.

## Steps

### Step 0: Set up, check the dependencies, record baselines (no commit)

1. Create the worktree and scratch folder, run the drift check.
2. Preconditions (each must hold, else STOP: a dependency WP is not merged):
   `test ! -f .claude/hooks/check-changelog-changed.sh && test -f tools/changelog_from_commits.py` (WP102);
   `grep -c '"matcher": "Bash|PowerShell"' .claude/settings.json` prints 1 or more (WP103);
   `test -f tools/tests/test_process_faction_map.py` (WP137; if only this one fails, ask the integrator
   whether WP137 merged: no file here depends on it, so the integrator may waive it).
3. Install snapshot (read-only):
   ```
   G="$(cygpath -u "$BANNERLORD_GAME_DIR")/Modules"
   stat -c '%Y %s %n' "$G/TAOM_Map/ModuleData/settlements.xml" "$G/TAOM_Map/ModuleData/Languages/DE/loc_settlements.xml" "$G/LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml" "$G/LOTRLOME_Armory/ModuleData/weapon_descriptions.xslt" > ../141-scratch/install-before.txt
   ```
4. `dotnet restore TAOM.sln`, Build, Test; record the totals (if they differ from the count the
   integrator recorded in `plans/README.md`, keep yours and report both).
5. Tool-test failure list: `python -m unittest discover -s tools/tests -t . 2>&1 | grep -aE "^(FAIL|ERROR): [A-Za-z_.0-9]+ \(" | sort > ../141-scratch/tools-baseline.txt`
   and note the `Ran N tests` line (the grep's shape keeps out the `ERROR: ...` lines tools print, which
   carry random temp paths). Measured at planning on `1b00d1acd` with these four entries, all pre-existing:
   `test_audit_map_scene_memory` (loader error), `test_applying_every_spec_is_a_no_op` (A7 fixes it), and the
   two `test_check_generator_item_refs` tests named in "Current state". Later WPs may change the list; the
   file you write is the baseline.
6. `bash tools/test_hooks.sh > ../141-scratch/hooks-baseline.txt 2>&1; tail -3 ../141-scratch/hooks-baseline.txt`.
7. `python tools/lint_docs.py > ../141-scratch/lint-baseline.txt 2>&1`.

**Verify**: the preconditions hold; Build exit 0; Test 0 failed; four baseline files exist.

---

## Part A: tool safety fixes (8 commits)

### Step A1: the translator's cap is a cap, and `--sync-ids --seed-only` seeds for free (a9f2d7eda, 71fea53c8)

Method: hand-apply (the 1.4.x file differs from the 1.5.x parent by about 144 lines). First run
`grep -c 'args.max_entries is not None' tools/translate_with_claude.py; grep -n 'add_argument("--seed-only"' tools/translate_with_claude.py; test -f tools/tests/test_translate_caps_and_seed.py && echo TESTFILE`.
- **Case L** (expected; the count is 1 or more, a `--seed-only` line prints and `TESTFILE` prints: WP135 step
  A7 landed all of this): do NOT touch the translator, its test file or the C# message, and do NOT copy the
  test over WP135's newer one. Run `python -m unittest tools.tests.test_translate_caps_and_seed`, confirm `OK`,
  make no commit, record "A1 landed by WP135" in the hand-off and go to A2.
- **Case N** (the count is 0 and no `--seed-only` line prints): do every item below.
- **Case W** (anything else: a seed-only mode without the cap fix): in GREEN do only items 2.1, 2.2,
  2.4, the second `if` of 2.5 and 2.7 (skip any item whose change is already present); delete the
  `SeedOnly` class from the copied test file; skip the C# message (WP135 owns that surface); record
  "seed-only semantics left as WP135 landed them" in the hand-off.

1. RED: `git show 71fea53c8:tools/tests/test_translate_caps_and_seed.py > tools/tests/test_translate_caps_and_seed.py`
   (6 tests; it patches every provider, cache and write function to raise, so no network call is
   possible). `python -m unittest tools.tests.test_translate_caps_and_seed` fails (main takes no argv,
   the zero cap is ignored).
2. GREEN, in `tools/translate_with_claude.py` (find each anchor by content):
   1. `def main():` becomes `def main(argv=None):`
   2. The `--max-entries` line becomes:
      ```python
          p.add_argument("--max-entries", type=int, default=None,
                         help="Cap API entries for testing; 0 is a cap of zero (no provider call)")
      ```
   3. (Case N) after the three-line `--sync-ids` argument add:
      ```python
          p.add_argument("--seed-only", action="store_true",
                         help="With --sync-ids: seed the missing ids and stop, before discovering or "
                              "translating anything (free; translation is a separate paid run)")
      ```
   4. `args = p.parse_args()` becomes `args = p.parse_args(argv)`.
   5. Directly after `p.error("Specify either --dry-run or --apply")` add:
      ```python
          if args.seed_only and not args.sync_ids:
              p.error("--seed-only is only valid with --sync-ids")
          if args.max_entries is not None and args.max_entries < 0:
              p.error("--max-entries must be 0 or more")  # a negative slice would send all but one entry to the provider
      ```
   6. (Case N) inside the `if args.sync_ids:` block, after the line that prints `Ids seeded`, add:
      ```python
              if args.seed_only:
                  return 0
      ```
   7. `if args.max_entries and len(need_llm) > args.max_entries:` becomes
      `if args.max_entries is not None and len(need_llm) > args.max_entries:`
3. (Case N) In `LanguageFileCoverageTests.cs` replace the last message line with:
   ```csharp
               "\n\nFix (free, seeds the English rows only): python tools/translate_with_claude.py " +
               "--lang <L> --module TAOM --sync-ids --seed-only --apply\n" +
               "Translating the seeded rows is a separate paid run.");
   ```

**Verify** (Case L: only the `OK` above, then go to A2): `python -m unittest tools.tests.test_translate_caps_and_seed` prints `Ran 6 tests` (Case W: 3)
and `OK`; Build exit 0; Filtered (`LanguageFileCoverageTests`) 0 failed.
Commit (none in Case L): `fix(tools): v2.0.28 - translator: --max-entries 0 is zero, free seeding` (Case W:
`fix(tools): v2.0.28 - translator: --max-entries 0 is a cap of zero`). Body: a zero cap used to mean no
cap on paid calls; a negative cap is refused; `--sync-ids --seed-only --apply` seeds English rows
without a provider call. Port of #804, #812, #813 (tool half). Ported-from `a9f2d7eda...` and
`71fea53c8...` (full SHAs in "Disposition").

### Step A2: the starter kit refuses to delete another author's rows and writes all or nothing (a9f2d7eda, 71fea53c8)

1. RED: in `tools/tests/test_generate_starter_kit.py` add `from unittest import mock` after
   `from pathlib import Path`, then insert, immediately before `class TestPiecesFile`, the block
   `git show 71fea53c8:tools/tests/test_generate_starter_kit.py | sed -n '/^class TestForeignRows/,/^class TestPiecesFile/p' | sed '$d'`
   (classes `TestForeignRows`, `_tree`, `TestRefusalsWriteNothing`, `TestVerifyExitCodes`,
   `TestMissingArmory`: 17 tests; they use `_plan_in`, `WD_XSLT` and `gk.*`, all on this line). Run the
   module: most new tests fail (`foreign_rows` missing, refusals half-write, `--verify` exit codes); a
   few, such as the plain missing-Armory run returning 0, already pass.
2. GREEN: `git diff a9f2d7eda^ 71fea53c8 -- tools/generate_starter_kit.py > ../141-scratch/a2.patch`
   (only those two commits touch the file in that range) then `git apply -3 ../141-scratch/a2.patch`.
   Planning checked it with `git apply --check --cached` against the 1.4.x content: all 16 hunks apply
   with offsets, no conflict. If a later WP changed the file and a conflict appears, keep the 1.4.x side
   of any `RETIRED_DONORS`, `ASSET_REPO` or `_refuse_shrink` text and the 1.5.x side of every guard
   hunk. Restore the shebang:
   `sed -i '1s|^#!/usr/bin/env python$|#!/usr/bin/env python3|' tools/generate_starter_kit.py`.
3. Doc: in `docs/features/starting-equipment-tuning.md` line 121 (the `generate_starter_kit.py` row),
   after "a folder the Armory does not register." add: ` It also refuses to rewrite or revert a marker
   block holding another author's rows in any of the three files (the two stylesheets and the pieces
   file), computes and parses every new text before any write, and `--verify` exits 2 when it checked
   nothing (a missing Armory) or crashed, never 1.`

**Verify**: `python -m unittest tools.tests.test_generate_starter_kit` OK; `grep -c "def foreign_rows\|def _write_all\|def _report_foreign" tools/generate_starter_kit.py` prints 3; `head -1 tools/generate_starter_kit.py` is `#!/usr/bin/env python3`.
Commit: `fix(tools): v2.0.28 - starter kit keeps foreign rows, writes all or none`. Ported-from both.

### Step A3: the item generators leave the `lotraom-assets` mirror alone unless named (798e4d0bf, 72cad16b1)

Method: re-implement (this line has no `_gamedir.armory_trees`).
1. RED, `tools/tests/test_generate_starter_kit.py`, after `TestVerifyExitCodes`:
   ```python
   class TestLiveArmoryOnlyByDefault(unittest.TestCase):
       """Sessions never touch the lotraom-assets mirror; it is checked or written only when named."""

       def _verified_trees(self, *flags):
           with tempfile.TemporaryDirectory() as tmp, tempfile.TemporaryDirectory() as mirror:
               (Path(mirror) / "ModuleData").mkdir()
               argv = ["generate_starter_kit.py", "--armory-path", tmp, "--quiet", "--verify", *flags]
               with mock.patch.object(sys, "argv", argv), mock.patch("builtins.print"), \
                       mock.patch.object(gk, "DEFAULT_ASSET_REPO", Path(mirror)), \
                       mock.patch.object(gk, "build_plan", return_value=object()), \
                       mock.patch.object(gk, "verify_plan", return_value=[]) as verify:
                   self.assertEqual(gk.main(), 0)
               return [Path(c.args[1]) for c in verify.call_args_list], Path(tmp), Path(mirror)

       def test_without_asset_repo_only_the_live_armory_is_checked(self):
           trees, live, _ = self._verified_trees()
           self.assertEqual(trees, [live / "ModuleData"])

       def test_naming_the_mirror_checks_it_too(self):
           with tempfile.TemporaryDirectory() as named:
               (Path(named) / "ModuleData").mkdir()
               trees, live, _ = self._verified_trees("--asset-repo", named)
           self.assertEqual(trees, [live / "ModuleData", Path(named) / "ModuleData"])
   ```
   and in `tools/tests/test_ranged_ladder.py` class `GeneratorTests`, after
   `test_missing_mirror_is_reported_and_skipped`:
   ```python
       def test_without_asset_repo_the_mirror_is_never_written(self):
           # Even with the example mirror path pointed at a real mirror, a run that names none leaves it alone.
           argv = ["--game-modules", str(self.modules), "--spec", str(self.spec_path), "--moduledata", str(self.md)]
           from unittest import mock
           with mock.patch.object(self.gen, "DEFAULT_ASSET_REPO", self.mirror):
               self.assertEqual(self.gen.main([*argv, "--apply"]), 0)
               self.assertEqual(self.gen.main([*argv, "--verify"]), 0)
           self.assertTrue(self._elf_file().exists())
           self.assertFalse(self._elf_file(self.mirror / "ModuleData").exists())
   ```
   `test_without_asset_repo_only_the_live_armory_is_checked` and the ranged ladder test fail today (the
   patched default is the mirror, so it is written and verified); `test_naming_the_mirror_checks_it_too`
   passes before and after (it pins that a named mirror still works).
2. GREEN, `tools/generate_starter_kit.py`: line 14 becomes
   `WHAT IT WRITES (the live Armory; the lotraom-assets mirror too only with --asset-repo)`; line 46's
   comment becomes `# write to the live Armory`; keep `DEFAULT_ASSET_REPO` (now the `--help` example);
   the two arguments become
   ```python
       ap.add_argument("--asset-repo", default=None,
                       help=f"also update this second Armory copy (e.g. {DEFAULT_ASSET_REPO}); off by default")
       ap.add_argument("--no-asset-repo", action="store_true", help="the default; kept so older commands still run")
   ```
   and the target block becomes
   ```python
       targets = [Path(sources.armory) / "ModuleData"]
       asset_repo = None if args.no_asset_repo else args.asset_repo
       if asset_repo is not None and Path(asset_repo).exists():
           targets.append(Path(asset_repo) / "ModuleData")
       elif asset_repo is not None:
           print(f"(asset repo {asset_repo} not present; live Armory only)")
   ```
   `tools/generate_ranged_ladder_items.py`: line 12 becomes
   `WHAT IT WRITES (the live Armory; the lotraom-assets mirror too only with --asset-repo)`, line 38's
   comment `# write to the live Armory`; the argument becomes
   ```python
       ap.add_argument("--asset-repo", default=None,
                       help=f"also update this lotraom-assets LOTRLOME_Armory mirror (e.g. {DEFAULT_ASSET_REPO}); "
                            "off by default, and skipped with a warning when absent")
   ```
   and lines 316-321 become
   ```python
       trees = [("armory", armory_md)]
       if args.asset_repo is not None:
           mirror_md = Path(args.asset_repo) / "ModuleData"
           if mirror_md.is_dir():
               trees.append(("mirror", mirror_md))
           else:
               print(f"WARNING: assets mirror not found at {args.asset_repo}; only the live Armory is touched")
   ```
3. Docs (keep the v1.4 path where one is named):
   - `starting-equipment-tuning.md` 88-89: "in the live `LOTRLOME_Armory` module and the `lotraom-assets`
     mirror, keyed by" becomes "in the live `LOTRLOME_Armory` module (the `lotraom-assets` mirror only
     with `--asset-repo`), keyed by"; line 121: "in the live Armory and the mirror." becomes "in the live
     Armory (the mirror only with `--asset-repo`)."; line 135 becomes
     ``- `python tools/generate_starter_kit.py --verify` on the live Armory (the `lotraom-assets` mirror only with `--asset-repo`).``;
     line 190: "(live install and `E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory`)" becomes "(live install;
     the `E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory` mirror only with `--asset-repo`)".
   - `ranged-ladders.md` 139: "(live Armory + lotraom-assets mirror)" becomes "(live Armory; the
     lotraom-assets mirror only with --asset-repo)"; 207: "into the live Armory and the mirror" becomes
     "into the live Armory, the mirror only with `--asset-repo`"; 232: "the generator end to end (both
     trees," becomes "the generator end to end (both trees when `--asset-repo` names the mirror, the
     mirror untouched without it,".

**Verify**: `python -m unittest tools.tests.test_generate_starter_kit tools.tests.test_ranged_ladder` OK;
`grep -c 'default=None' tools/generate_starter_kit.py tools/generate_ranged_ladder_items.py` prints at least 1 per file.
Commit: `fix(tools): v2.0.28 - item generators leave the assets mirror alone`. Body: names the
2026-09-22 rule; `--no-asset-repo` stays accepted. Ported-from `798e4d0bf`, `72cad16b1`.

### Step A4: the tavern mercenary tool is a dry run by default and can finish a run (a9f2d7eda, 71fea53c8, 44e9edd48)

1. RED: `git show 71fea53c8:tools/tests/test_generate_tavern_mercenaries.py > tools/tests/test_generate_tavern_mercenaries.py`
   (9 tests). Every case patches `TROOPS_DIR`, `SPCULTURES` and `PICKS` to a temp tree first, so the old
   tool writes only there. `python -m unittest tools.tests.test_generate_tavern_mercenaries` fails.
2. GREEN: `git show 71fea53c8:tools/oneoff/generate_tavern_mercenaries.py > tools/oneoff/generate_tavern_mercenaries.py`
   (the 1.5.x end state: argparse, `plan_copies`, `plan_repoint` iterating last to first, `check_parses`,
   dry run unless `--apply`, only changed files written, the `mistymountainorcs` pick on `goblin_fighter`
   with its comment). Then restore this line's data:
   - the three pool-comment lines starting `# Main/Features/TroopProgression/RecruitmentPools/ (weight 2 for Lothlorien and Abanissa`
     become the 1.4.x text:
     ```
     # Main/Features/TroopProgression/RecruitmentPools/ (weight 2 for Lothlorien, 3 for Umbar/Harad --
     # those pools have no weight-1 entry). Excluded by rule: creature mounts (taom_spider_creature)
     # and level-51 legendaries (rivendell_knight_golden_flower).
     ```
   - the comment `# Since the 2026-10-10 Harad rework ...` and the `shaghana`/`abanissa` lines become:
     ```python
         "shaghana": [("harad_noble", "harad", "[Harad] Hired Youngblood of the Serpent")],
         "abanissa": [("harad_noble", "harad", "[Harad] Hired Youngblood of the Serpent")],
     ```
3. Doc, `docs/features/tavern-mercenaries.md`: line 125 becomes
   ``| `mistymountainorcs` | `goblin_fighter` (the goblin tree's copy; `troops_mistymountainorcs.xml` was removed in `f577bd547`) | 1 |``;
   replace the whole line 183 (it starts ``4. Run `python tools/oneoff/generate_tavern_mercenaries.py` ``) with:
   ```
   4. Run `python tools/oneoff/generate_tavern_mercenaries.py` for a dry run that reports the copies it
      would add, then `python tools/oneoff/generate_tavern_mercenaries.py --apply` to write. It parses
      every new file before writing any, writes only files whose bytes changed, and is idempotent
      (existing copies are skipped).
   ```
4. Read-only smoke run (now safe; it reads this worktree's repo data): `python tools/oneoff/generate_tavern_mercenaries.py`.

**Verify**: the module test prints `Ran 9 tests` and `OK`; `grep -c "harad_redserp_noble\|harad_pz_levy\|mistymountainorcs_fighter" tools/oneoff/generate_tavern_mercenaries.py` prints 0;
the smoke run's last line is `0 files changed (dry run, pass --apply to write)` (if it reports a change,
do NOT apply: paste the output into the hand-off and continue; the unit tests are the gate);
`git status --porcelain Main/` prints nothing.
Commit: `fix(tools): v2.0.28 - tavern mercenary tool: dry run, finishes a run`. Ported-from all three.

### Step A5: the enlistment seed marker parses, and the fallback kit table is gated (44e9edd48, 71fea53c8)

1. RED: create `tools/tests/test_generate_enlistment_rosters.py` (if another WP created it, append only the class):
   ```python
   #!/usr/bin/env python3
   """Unit tests for tools/generate_enlistment_rosters.py.

   Run:  python -m unittest tools.tests.test_generate_enlistment_rosters
   """
   import os
   import sys
   import unittest
   import xml.etree.ElementTree as ET

   sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

   import generate_enlistment_rosters as ger  # noqa: E402


   class SeedMarkerTests(unittest.TestCase):
       def test_the_seed_marker_is_a_legal_xml_comment(self):
           # XML forbids "--" inside a comment, and the marker once named the flag, so every
           # --seed-missing run refused its own output as malformed (#798).
           ET.fromstring(f'<EquipmentRosters>\n    {ger.SEED_MARKER}\n</EquipmentRosters>')

       def test_the_append_path_writes_the_constant(self):
           with open(ger.__file__, encoding="utf-8") as fh:
               self.assertNotIn("generate_enlistment_rosters.py --seed-missing -->", fh.read())


   if __name__ == "__main__":
       unittest.main()
   ```
   and in `tools/tests/test_check_generator_item_refs.py`, before `class ValidatorWiringTests` (or before
   `LiveInstallGateTests` if that class is absent):
   ```python
   class EnlistmentGeneratorSpecTests(unittest.TestCase):
       def test_the_enlistment_fallback_table_is_registered_and_collects_only_item_ids(self):
           specs = [s for s in cg.GENERATORS if s.path == "tools/generate_enlistment_rosters.py"]
           self.assertEqual(1, len(specs))
           self.assertEqual({"DEFAULT_ROSTER_ITEMS"}, set(specs[0].tables))
           refs = cg.collect(specs[0])
           self.assertIn("rohan_militia_tunic_a", refs)
           self.assertIn("wm_rohan_ws_sword_a01", refs)
           for label in ("Item0", "Body", "Head", "Leg", "recruit", "soldier", "infantry", "archer"):
               self.assertNotIn(label, refs)
   ```
   All three fail today.
2. GREEN: in `tools/generate_enlistment_rosters.py` after the `RANKS` line add
   ```python

   # The marker --seed-missing writes above the rosters it appends. XML forbids a double hyphen
   # inside a comment, so it must not spell the flag.
   SEED_MARKER = '<!-- seeded by generate_enlistment_rosters.py (seed-missing) -->'
   ```
   and change line 936 to `        insert = (f'\n    {SEED_MARKER}\n'`. In `tools/check_generator_item_refs.py`
   after the `generate_starter_armor.py` spec add
   ```python
       # tuple_from=1 skips the slot label of each (slot, id) row of DEFAULT_ROSTER_ITEMS.
       GeneratorSpec("tools/generate_enlistment_rosters.py", "tables", ("DEFAULT_ROSTER_ITEMS",), tuple_from=1),
   ```
   (`ISSUE_LAUNCHER` does not exist on this line; keep it out.)

**Verify**: `python -m unittest tools.tests.test_generate_enlistment_rosters tools.tests.test_check_generator_item_refs 2>&1 | grep -aE "^(FAIL|ERROR): [A-Za-z_.0-9]+ \(|^Ran "`
lists no failure the Step 0 file lacks (`LiveInstallGateTests.test_no_generator_writes_a_retired_id` and
`ValidatorWiringTests.test_missing_id_becomes_a_warning_under_the_code` may already be there, for other
generators, and are not yours to fix). The gate now also checks the 24 fallback ids against the installed
modules, so read its findings directly:
`python -c "import os,sys,pathlib; sys.path.insert(0,'tools'); import check_generator_item_refs as c; g=pathlib.Path(os.environ['BANNERLORD_GAME_DIR'])/'Modules'; [print(f.generator,f.unresolved) for f in c.check(c.build_registry(g))]"`
must print no `tools/generate_enlistment_rosters.py` line; `python tools/validate_moduledata.py` shows no ERROR the
WP101 baseline lacks.
Commit: `fix(tools): v2.0.28 - enlistment seed marker parses; fallback kit gated`. Ported-from `44e9edd48`, `71fea53c8`.

### Step A6: troop tools: battle-set weapons only, quote-proof militia binding, atomic armour fix (8465cc984, b904e02a3)

1. RED:
   - `git diff 8465cc984^ 8465cc984 -- tools/tests/test_fix_upgrade_armour_regressions.py > ../141-scratch/a6-1.patch && git apply -3 ../141-scratch/a6-1.patch`
     (adds `test_a_file_that_would_not_parse_leaves_every_file_unwritten`).
   - In `tools/tests/test_militia_elite_bonus.py` class `EliteMilitiaBindingTests`, after
     `test_both_encodings_split_basic_from_elite`, add the method from 8465cc984:
     ```python
         def test_a_single_quoted_binding_is_read_like_the_validator_reads_it(self):
             # taom_schema's militia reader takes either quote and this one took only double quotes,
             # so the two could disagree on who is militia.
             with open(os.path.join(self.md, "taom_spcultures.xml"), "a", encoding="utf-8") as f:
                 f.write("<Culture id='z' ranged_elite_militia_troop='NPCCharacter.z_arc_vet' />\n")
             rb._militia_ids_cache.clear()
             rb._elite_militia_ids_cache.clear()
             self.assertIn("z_arc_vet", rb.militia_troop_ids(self.md))
             self.assertIn("z_arc_vet", rb.elite_militia_troop_ids(self.md))
     ```
   - In `tools/tests/test_rebalance_equipment.py` class `WeaponClassExtractionTests` add:
     ```python
         def test_a_weapon_only_in_a_civilian_roster_does_not_count(self):
             # imladris_recruit holds its only bow in an EquipmentRoster civilian="true", and a civilian
             # set never cross-draws with a battle set (b904e02a3, #617).
             npc = ET.fromstring(
                 '<NPCCharacter id="t" name="T" level="6"><Equipments>'
                 '<EquipmentRoster><equipment slot="Item0" id="Item.sword_1h" /></EquipmentRoster>'
                 '<EquipmentRoster civilian="true"><equipment slot="Item0" id="Item.bow_a" /></EquipmentRoster>'
                 '</Equipments></NPCCharacter>')
             self.assertEqual(rb.troop_weapon_classes(npc, ITEM_CLASSES), {"OneHanded"})

         def test_an_equipment_type_civilian_set_does_not_count(self):
             npc = ET.fromstring(
                 '<NPCCharacter id="t" name="T" level="6"><Equipments>'
                 '<EquipmentSet id="x" equipmentType="Civilian"><equipment slot="Item0" id="Item.bow_a" /></EquipmentSet>'
                 '<EquipmentRoster><equipment slot="Item0" id="Item.spear_a" /></EquipmentRoster>'
                 '</Equipments></NPCCharacter>')
             self.assertEqual(rb.troop_weapon_classes(npc, ITEM_CLASSES), {"Polearm"})

         def test_an_override_written_under_equipments_still_counts(self):
             # BasicCharacterObject.Deserialize (v1.4.8, 365 and 412) applies an <equipment> written
             # straight under <Equipments> to every set, so it is a battle weapon.
             npc = ET.fromstring(
                 '<NPCCharacter id="t" name="T" level="6"><Equipments>'
                 '<equipment slot="Item1" id="Item.javelin_a" />'
                 '<EquipmentRoster><equipment slot="Item0" id="Item.sword_1h" /></EquipmentRoster>'
                 '</Equipments></NPCCharacter>')
             self.assertEqual(rb.troop_weapon_classes(npc, ITEM_CLASSES), {"OneHanded", "Throwing"})
     ```
     The armour test, the militia test and the first two civilian tests fail today; the third civilian
     test passes before and after (it pins that the filter keeps the override, unlike 1.5.x's
     `rl.battle_sets`).
2. GREEN:
   - `git diff 8465cc984^ 8465cc984 -- tools/fix_upgrade_armour_regressions.py > ../141-scratch/a6-2.patch && git apply -3 ../141-scratch/a6-2.patch`.
   - `tools/rebalance_troops.py:403`: `militia_troop"?\s*(?:=\s*"|>)\s*` becomes `militia_troop["\']?\s*(?:=\s*["\']|>)\s*`.
   - Replace `troop_weapon_classes` (287-301) with:
     ```python
     def troop_weapon_classes(npc_elem, item_classes):
         """Set of skill classes ('OneHanded'/'TwoHanded'/'Polearm'/'Bow'/'Crossbow'/
         'Throwing'/'Arrows'/'Bolts'/'Shield') the troop carries into battle, from the
         weapon slots (Item0..Item3). A civilian set (an EquipmentRoster civilian="true"
         or an EquipmentSet equipmentType="Civilian", the shapes BasicCharacterObject
         and MBEquipmentRoster read as civilian on v1.4.8) never cross-draws with a
         battle set, so a weapon only there does not count: imladris_recruit holds its
         only bow in a civilian roster. An <equipment> written straight under
         <Equipments> overrides every set, so it counts."""
         civilian = {id(eq)
                     for es in list(npc_elem.iter('EquipmentRoster')) + list(npc_elem.iter('EquipmentSet'))
                     if es.get('civilian') == 'true' or es.get('equipmentType') == 'Civilian'
                     for eq in es.iter('equipment')}
         classes = set()
         for eq in npc_elem.findall('.//equipment'):
             if id(eq) in civilian:
                 continue
             slot = eq.get('slot', '')
             if not slot.startswith('Item'):
                 continue
             item_id = (eq.get('id') or '').replace('Item.', '', 1)
             skill = item_classes.get(item_id)
             if skill:
                 classes.add(skill)
         return classes
     ```
   Do NOT take the rest of 8465cc984 or b904e02a3 (the #617 per-tier ladder rework, the translation sync
   tool, `planned_skill_edits` for templated troops: its premise is false on v1.4.8,
   `BasicCharacterObject.cs:337-357`).

**Verify**: `python -m unittest tools.tests.test_fix_upgrade_armour_regressions tools.tests.test_militia_elite_bonus tools.tests.test_rebalance_equipment` OK.
Commit: `fix(tools): v2.0.28 - troop rebalance skips civilian sets; atomic writes`. Ported-from `8465cc984`, `b904e02a3`.

### Step A7: the clan heraldry tool keeps each file's line endings (b75fbab07)

1. `git cherry-pick -n b75fbab07`. Expected conflict: `tools/README.md` only (`git checkout --ours tools/README.md && git add tools/README.md`);
   if it applied cleanly instead, `git checkout HEAD tools/README.md`. The tool and test apply cleanly
   (identical to the parent); the pick brings 7 new tests.
2. `git status --porcelain` lists only the tool and its test.

**Verify**: `python -m unittest tools.tests.test_clan_heraldry_specs` OK, including the no-op test that may
have failed in the Step 0 baseline.
Commit: `fix(tools): v2.0.28 - clan heraldry tool keeps the file's line endings`. Ported-from `b75fbab07`.

### Step A8: the Tripo texture converter has no live default and never overwrites silently (041808e5f)

1. RED: `git show 041808e5f:tools/tests/test_convert_tripo_prop_textures.py > tools/tests/test_convert_tripo_prop_textures.py`
   (14 tests; it skips itself without numpy or Pillow). Run: it fails.
2. GREEN: `git show 041808e5f:tools/oneoff/convert_tripo_prop_textures.py > tools/oneoff/convert_tripo_prop_textures.py`
   (end state: `--src`/`--dst` required, `--force`, one triple per texture set, the ORM map, the fixed AO
   pattern; it carries the converter hunks of c79a58521 and 71fea53c8). Doc: in
   `docs/reference/ue-to-bannerlord-asset-pipeline.md` line 91 replace "with `--src <export dir>`." with
   "with `--src <export dir> --dst <textures dir>` (both required; an existing output is replaced only
   with `--force`)."
3. Leave `tools/oneoff/blender_prep_tripo_prop.py` alone: 041808e5f's report clearing needs
   `tools/blender/blender_job.py`, which imports `tools/oneoff/kitbash_kits.py` (absent here), and no
   1.4.x tool polls that report.

**Verify**: `python -m unittest tools.tests.test_convert_tripo_prop_textures` prints `Ran 14 tests` and `OK`;
`grep -c "DEFAULT_DST" tools/oneoff/convert_tripo_prop_textures.py` prints 0.
Commit: `fix(tools): v2.0.28 - Tripo converter: no live default, no overwrite`. Ported-from `041808e5f`,
`71fea53c8`, `c79a585218ad22841c8db1f273e013385512b332`.

### Part A done criteria

- [ ] 8 commits (7 if A1 was Case L); `git log --format=%s bannerlord-1.4.5..HEAD` lists the subjects of the
      steps that committed.
- [ ] Tool failure list compared with Step 0: no entry the baseline lacks; the 64 new tool tests
      (Case W: 61; Case L: 58, A1's 6 having come with WP135) pass.
- [ ] Build exit 0; Test 0 failed with the Step 0 total.
- [ ] `python tools/lint_docs.py`: no new finding in the four Part A docs.

---

## Part B: map tool hardening (3 commits; needs Part A merged in this branch only for order)

### Step B1: the village tool refuses a taken name, checks culture and bound, and can evict cache rows (244c15d27, 87ffea9b2, af2b198e4, 4b207f951)

Method: hand-apply helpers (the 1.5.x file is 256 lines longer with 1.5.x content; take none of the
`Village(...)` rows, `MESHES` lines, `append_settlements`, `save_order_findings` or name-tag code).
1. RED, `tools/tests/test_add_map_villages.py`:
   - at the end of `CheckTests` (before `class BatchGeneralisationTests`) insert
     `git show 4b207f951:tools/tests/test_add_map_villages.py | sed -n '/    def test_check_names_a_retagged_culture/,/^class NameCollisionTests/p' | sed '$d'`
     (3 tests);
   - before `class BatchGeneralisationTests` insert
     `git show af2b198e4:tools/tests/test_add_map_villages.py | sed -n '/^class NameCollisionTests/,/^class BatchGeneralisationTests/p' | sed '$d'`
     (`NameCollisionTests`, `CacheEvictionTests`: 10 tests). The fixtures they use exist unchanged.
   Run the module: the 13 new tests fail.
2. GREEN, `tools/add_map_villages.py`:
   1. `import json` after `import datetime`.
   2. After the `LANGS = [...]` line: `CACHE_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "translation_cache")`.
   3. Before `^def resolve_position` (it follows `settlement_position`) insert
      `git show 4b207f951:tools/add_map_villages.py | sed -n '/^def master_attr/,/^def resolve_position/p' | sed '$d'`
      (`master_attr`, `master_village_bound`), with `insert_before.py`.
   4. Before `def loc_row` insert `git show af2b198e4:tools/add_map_villages.py | sed -n '/^def master_names/,/^def tagged_rows/p' | sed '$d'`
      (`master_names`, `name_collisions`, `evict_cached_names` with its missing-file guard).
   5. `check()`: docstring becomes `"""Findings (strings) for any table id whose data is missing, whose position drifted or whose name is taken,
      or whose master culture or bound settlement differs from the table's."""`; `findings = []` becomes
      `findings = name_collisions([(v.id, v.name) for v in villages], master_text)`; after the
      `no <Settlement> row` finding add
      ```python
              culture = master_attr(master_text, v.id, "culture")
              if culture is not None and culture != f"Culture.{v.culture}":
                  findings.append(f"{v.id}: settlements.xml has culture={culture} but the table says Culture.{v.culture}")
              bound = master_village_bound(master_text, v.id)
              if bound is not None and bound != f"Settlement.{v.bound}" and settlement_position(master_text, v.bound):
                  findings.append(f"{v.id}: settlements.xml binds it to {bound} but the table says Settlement.{v.bound}")
      ```
   6. `main()`: the `--check` help becomes "verify every VILLAGES id is fully present, positioned, and on
      its table culture and bound; exit 1 otherwise"; the OK message ends "positions, culture and bound
      match; no other settlement carries their names."; after `nl = detect_newline(master)` add
      ```python
          clashes = name_collisions([(v.id, v.name) for v in todo], master)
          if clashes:
              print("REFUSING: rename the row in VILLAGES first:\n  " + "\n  ".join(clashes), file=sys.stderr)
              return 1
      ```
   7. Module docstring: the `--check` paragraph ends "...drifted from the scene transform, or whose
      culture or bound differs from the table."; "same display name in all 12 (Tolkien proper nouns do not
      translate)" becomes "with the English display name in all 12 (a later
      `translate_with_claude.py --module TAOM_Map` run translates or transliterates it). A name another
      settlement already carries is refused, and named by `--check` (name_collisions)"; the `--check`
      usage line ends "... / drifted position / culture or bound mismatch".
3. If WP135 already added name-tag code to this file, keep it; the helpers above are independent of it.

**Verify**: `python -m unittest tools.tests.test_add_map_villages` OK (13 more tests than Step 0's count
for the module); `grep -c "^def name_collisions\|^def evict_cached_names\|^def master_attr\|^def master_village_bound\|^def master_names" tools/add_map_villages.py` prints 5.
Commit: `fix(tools): v2.0.28 - village tool refuses taken names, checks culture`. Ported-from `244c15d27`,
`87ffea9b2`, `af2b198e4`, `4b207f951` (body: the map-tool hunks only; no Dorwinion or other content).

### Step B2: the map renamer writes only renames, dry run first, cache first and master last (244c15d27, 87ffea9b2, af2b198e4)

1. RED: create `tools/tests/test_apply_map_village_names.py` exactly as below (13 tests; adapted from
   the 1.5.x `ApplyMapVillageNamesTests` without the BR/IT grammar-tag cases; BR and IT are not
   asserted, so it holds whether or not `tools/name_tags.py` exists). Every run points
   `BANNERLORD_GAME_DIR` at a temp tree BEFORE the script loads, so even today's script, which writes at
   import, writes only there. Run it: it fails (`main` missing, the load test sees writes).
   ```python
   #!/usr/bin/env python3
   """tools/Apply-MapVillageNames.py on a temp BANNERLORD_GAME_DIR tree (#1042).

   Run:  python -m unittest tools.tests.test_apply_map_village_names

   Only renames are written, the dry run writes nothing, a rename leaves the translation cache, a taken
   name or an unreadable file refuses before any write, and a run that fails part-way is finished by
   running it again. BR and IT are not asserted: they may carry grammar tags (tools/name_tags.py).
   """
   import contextlib
   import io
   import os
   import runpy
   import stat
   import tempfile
   import unittest
   from pathlib import Path

   P = "Settlements.Settlement.name."
   LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]
   UNTAGGED = [lang for lang in LANGS if lang not in ("BR", "IT")]
   APPLY = Path(__file__).resolve().parent.parent / "Apply-MapVillageNames.py"


   def loc_xml(rows):
       body = "".join(f'    <string id="{P}{i}" text="{t}" />\r\n' for i, t in rows)
       return f'<?xml version="1.0" encoding="utf-8"?>\r\n<base>\r\n  <strings>\r\n{body}  </strings>\r\n</base>\r\n'


   @contextlib.contextmanager
   def game_dir(td):
       old = os.environ.get("BANNERLORD_GAME_DIR")
       os.environ["BANNERLORD_GAME_DIR"] = td
       try:
           yield
       finally:
           if old is None:
               del os.environ["BANNERLORD_GAME_DIR"]
           else:
               os.environ["BANNERLORD_GAME_DIR"] = old


   class ApplyMapVillageNamesTests(unittest.TestCase):
       MASTER = (f'<S><Settlement id="village_RU8_4" name="{{={P}village_RU8_4}}Old" />'
                 f'<Settlement id="village_RU8_3" name="{{={P}village_RU8_3}}Old" />'
                 f'<Settlement id="village_RU8_2" name="{{={P}village_RU8_2}}Iôrig-Vargh" /></S>')
       ROWS = [("village_RU8_4", "Old"), ("village_RU8_2", "Ыыы")]

       def _tree(self, td, master=None):
           data = Path(td) / "Modules" / "TAOM_Map" / "ModuleData"
           cache = Path(td) / "cache"
           data.mkdir(parents=True)
           cache.mkdir()
           if master is not False:
               (data / "settlements.xml").write_bytes((self.MASTER if master is None else master).encode("utf-8"))
           for lang in LANGS:
               f = data / "Languages" / lang / "loc_settlements.xml"
               f.parent.mkdir(parents=True)
               f.write_bytes(loc_xml(self.ROWS).encode("utf-8"))
               (cache / f"{lang.lower()}.json").write_bytes(
                   ('{\n  "' + P + 'village_RU8_2": "Ыыы",\n  "' + P + 'village_RU8_4": "Altes"\n}').encode("utf-8"))
           return data, cache

       def _main(self, td, argv=("--apply",)):
           with game_dir(td), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
               ns = runpy.run_path(str(APPLY), run_name="apply_map_village_names")
               return ns["main"](list(argv), cache_dir=str(Path(td) / "cache"))

       def _read(self, data, cache):
           self.master_after = (data / "settlements.xml").read_text(encoding="utf-8")
           self.cache_after = {lang: (cache / f"{lang.lower()}.json").read_text(encoding="utf-8") for lang in LANGS}
           return {lang: (data / "Languages" / lang / "loc_settlements.xml").read_text(encoding="utf-8") for lang in LANGS}

       def _run(self, td, argv=("--apply",), master=None):
           data, cache = self._tree(td, master)
           self.assertEqual(self._main(td, argv), 0)
           return self._read(data, cache)

       def test_script_LoadingTheModuleRunsNothing(self):
           # Before #1042 the script renamed at import: loading it wrote 13 live files.
           with tempfile.TemporaryDirectory() as td:
               self._tree(td)
               before = {p: p.read_bytes() for p in Path(td).rglob("*") if p.is_file()}
               with game_dir(td), contextlib.redirect_stdout(io.StringIO()):
                   runpy.run_path(str(APPLY), run_name="apply_map_village_names")
               after = {p: p.read_bytes() for p in Path(td).rglob("*") if p.is_file()}
           self.assertEqual(before, after)

       def test_script_RenamedId_UpdatesTheMasterAndTheRowsAndLeavesTheCache(self):
           with tempfile.TemporaryDirectory() as td:
               out = self._run(td)
           self.assertIn('village_RU8_4}Iôrig-Mukh"', self.master_after)
           self.assertIn('village_RU8_3}Iôrig-Tün"', self.master_after)
           for lang in UNTAGGED:
               self.assertIn('text="Iôrig-Mukh"', out[lang], lang)
           for lang in LANGS:
               self.assertNotIn("village_RU8_4", self.cache_after[lang], lang)

       def test_script_UnchangedName_KeepsItsTranslationAndCacheRow(self):
           with tempfile.TemporaryDirectory() as td:
               out = self._run(td)
           self.assertIn('text="Ыыы"', out["RU"])
           self.assertIn("village_RU8_2", self.cache_after["RU"])

       def test_script_DryRun_WritesNothing(self):
           with tempfile.TemporaryDirectory() as td:
               out = self._run(td, argv=())
           self.assertIn('village_RU8_4}Old"', self.master_after)
           self.assertIn('text="Old"', out["DE"])
           self.assertIn("village_RU8_4", self.cache_after["DE"])

       def test_script_SecondRun_ChangesNothing(self):
           with tempfile.TemporaryDirectory() as td:
               data, cache = self._tree(td)
               self.assertEqual(self._main(td), 0)
               first, master1 = self._read(data, cache), self.master_after
               self.assertEqual(self._main(td), 0)
               second, master2 = self._read(data, cache), self.master_after
           self.assertEqual(first, second)
           self.assertEqual(master1, master2)

       def test_script_MasterWithABom_KeepsExactlyOneBom(self):
           with tempfile.TemporaryDirectory() as td:
               self._run(td, master="\ufeff" + self.MASTER)
               raw = (Path(td) / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml").read_bytes()
           self.assertTrue(raw.startswith(b"\xef\xbb\xbf<S>"))

       def test_script_Apply_BacksUpEachChangedFileOutsideTheXmlGlob(self):
           with tempfile.TemporaryDirectory() as td:
               self._run(td)
               data = Path(td) / "Modules" / "TAOM_Map" / "ModuleData"
               master_baks = list(data.glob("settlements.xml.bak_villagenames_*"))
               de_baks = list((data / "Languages" / "DE").glob("loc_settlements.xml.bak_villagenames_*"))
               self.assertEqual(len(master_baks), 1)
               self.assertIn('village_RU8_4}Old"', master_baks[0].read_text(encoding="utf-8"))
               self.assertEqual(len(de_baks), 1)

       def test_script_LocFileThatDoesNotParse_RefusesAndWritesNothing(self):
           with tempfile.TemporaryDirectory() as td:
               data, cache = self._tree(td)
               (data / "Languages" / "DE" / "loc_settlements.xml").write_bytes(b'<base><strings><string id="x" text="Old" />')
               self.assertEqual(self._main(td), 1)
               out = self._read(data, cache)
           self.assertIn('village_RU8_4}Old"', self.master_after)
           self.assertIn('text="Old"', out["FR"])
           self.assertIn("village_RU8_4", self.cache_after["FR"])

       def test_script_AWriteThatFails_IsFinishedByARerun(self):
           # The master decides what a run renames, so it is written last.
           with tempfile.TemporaryDirectory() as td:
               data, cache = self._tree(td)
               de = data / "Languages" / "DE" / "loc_settlements.xml"
               os.chmod(de, stat.S_IREAD)
               try:
                   with self.assertRaises(PermissionError):
                       self._main(td)
               finally:
                   os.chmod(de, stat.S_IREAD | stat.S_IWRITE)
               self.assertIn('village_RU8_4}Old"', (data / "settlements.xml").read_text(encoding="utf-8"))
               self.assertEqual(self._main(td), 0)
               out = self._read(data, cache)
           self.assertIn('village_RU8_4}Iôrig-Mukh"', self.master_after)
           for lang in LANGS:
               self.assertIn("Iôrig-Mukh", out[lang], lang)
               self.assertNotIn("village_RU8_4", self.cache_after[lang], lang)

       def test_script_NewNameAnotherSettlementCarries_RefusesAndWritesNothing(self):
           master = self.MASTER.replace("</S>", f'<Settlement id="castle_Z1" name="{{={P}castle_Z1}}Iôrig-Mukh" /></S>')
           with tempfile.TemporaryDirectory() as td:
               data, cache = self._tree(td, master)
               self.assertEqual(self._main(td), 1)
               out = self._read(data, cache)
           self.assertIn('village_RU8_4}Old"', self.master_after)
           self.assertIn('text="Old"', out["DE"])
           self.assertIn("village_RU8_4", self.cache_after["DE"])

       def test_script_TwoTableIdsSwappingNames_IsNotAClash(self):
           swapped = (f'<S><Settlement id="village_RU8_4" name="{{={P}village_RU8_4}}Iôrig-Tün" />'
                      f'<Settlement id="village_RU8_3" name="{{={P}village_RU8_3}}Iôrig-Mukh" /></S>')
           with tempfile.TemporaryDirectory() as td:
               self._run(td, master=swapped)
           self.assertIn('village_RU8_4}Iôrig-Mukh"', self.master_after)
           self.assertIn('village_RU8_3}Iôrig-Tün"', self.master_after)

       def test_script_MasterWithNoTableId_Exits2(self):
           with tempfile.TemporaryDirectory() as td:
               self._tree(td, master=f'<S><Settlement id="town_Q1" name="{{={P}town_Q1}}Q" /></S>')
               self.assertEqual(self._main(td), 2)

       def test_script_MissingMaster_Exits2(self):
           with tempfile.TemporaryDirectory() as td:
               self._tree(td, master=False)
               self.assertEqual(self._main(td), 2)


   if __name__ == "__main__":
       unittest.main()
   ```
2. GREEN: `git show af2b198e4:tools/Apply-MapVillageNames.py > tools/Apply-MapVillageNames.py` (the 1.5.x
   end state; its `NAMES` equals this line's). Then, only if `tools/name_tags.py` does not exist:
   - delete the line `import name_tags  # noqa: E402`;
   - `def patch(text, ids, mode, table, lang=None):` becomes `def patch(text, ids, mode):`, and its loc
     branch's `subn` line becomes
     `            text, n = pat.subn(lambda m, v=new: m.group(1) + v + m.group(3), text)`;
   - in `main()` delete `    table = name_tags.load_table()`, change the call to
     `new, n = patch(text, ids, "master" if key is None else "loc")`, and delete the final
     `print("Then run python tools/generate_name_tags.py --write: ...")` line;
   - in the docstring (lines 16-17), "...all with the English name; the translation" / "backlog (#763)
     later translates it, or transliterates it in JP, KO, CN and RU" becomes "...all with the English
     name; a later" / "translate_with_claude.py --module TAOM_Map run translates it, or transliterates it
     in JP, KO, CN and RU" (two lines, same indentation).
   Check: `grep -c "name_tags" tools/Apply-MapVillageNames.py` prints 0 when `tools/name_tags.py` is absent.
3. Prove the table did not move:
   ```
   python - <<'EOF'
   import ast, subprocess
   def names(src):
       for node in ast.parse(src).body:
           if isinstance(node, ast.Assign) and getattr(node.targets[0], "id", "") == "NAMES":
               return ast.literal_eval(node.value)
   old = subprocess.run(["git", "show", "HEAD:tools/Apply-MapVillageNames.py"], capture_output=True, text=True, encoding="utf-8").stdout
   new = open("tools/Apply-MapVillageNames.py", encoding="utf-8").read()
   print(len(names(old)), len(names(new)), names(old) == names(new))
   EOF
   ```

**Verify**: `python -m unittest tools.tests.test_apply_map_village_names` prints `Ran 13 tests` and `OK`;
the NAMES check prints `438 438 True`; `grep -c '^if __name__ == "__main__":' tools/Apply-MapVillageNames.py` prints 1;
`git status --porcelain tools/translation_cache` prints nothing.
Commit: `fix(tools): v2.0.28 - map renamer writes only renames, dry run first`. Body: the old script reset
every translated name of its 438 ids on each run and evicted nothing; it now writes only ids whose English
changed, dry by default, refuses a taken name, cache first and master last, exit 2 on the wrong file.
Port of #1042, #1039. Ported-from the three.

### Step B3: the map docs describe the renamer as it now behaves (af2b198e4, 244c15d27, 87ffea9b2)

Replace text (no em or en dash in anything you write):
1. `docs/reference/taom-map-settlement-naming.md` line 11 (it starts "- For bulk renames, use") becomes:
   ```
   - For bulk renames, use [`tools/Apply-MapVillageNames.py`](../../tools/Apply-MapVillageNames.py). It does 13 files in one pass and preserves UTF-8 + CRLF; a second run changes nothing.
   - **It writes only real renames and evicts their cache rows (#1042).** A run rewrites an id only when its English `name=` in `settlements.xml` differs from `NAMES`, writes that id's 13 rows, and removes it from `tools/translation_cache/<lang>.json`: the cache keys on the string id alone, so the next translator run would otherwise write the old wording back. Before this it rewrote all 438 of its ids in all 12 languages on every run, which reset every translation made since. It runs dry by default; `--apply` writes the cache and the 12 loc files first and the master last, so a run that fails part-way is finished by running it again. It refuses (exit 1) a new name another settlement already carries, and exits 2 when the files are missing or none of its ids is in the master (the wrong file).
   ```
   In "When to use `Apply-MapVillageNames.py`": replace the line starting `4. **Dry-run is implicit**` with
   ``4. **Dry run first.** `python tools/Apply-MapVillageNames.py` lists the ids whose English changed. It aborts on duplicate names within the dict and refuses (exit 1) a new name another settlement already carries, checked against the renamed master so two ids can swap names.``;
   the line starting `5. **Run the script**` with
   ``5. **Apply** with `python tools/Apply-MapVillageNames.py --apply`: it writes the master and the 12 loc rows of each renamed id, backs up each changed file (`*.bak_villagenames_<timestamp>`, outside the `.xml` glob) and removes the renamed ids from `tools/translation_cache/<lang>.json`.``;
   the line starting `7. **In-game spot-check**` with
   ``7. **In-game spot-check:** launch Bannerlord, tour the campaign map, confirm names render. Switch language (DE/FR/JP): a renamed settlement shows its new English name until a `translate_with_claude.py --module TAOM_Map` run translates it. Translated names change only at the next game start or a language switch, never at a save load (v1.4.8 loads a language's strings in `MBTextManager.ChangeLanguage`).``
2. `docs/modding/settlements.md` line 338: after "preserves the file's UTF-8 and CRLF bytes." insert
   " It writes only the ids whose English changed, runs dry until `--apply`, refuses a name another
   settlement already carries, and removes each renamed id from `tools/translation_cache/<lang>.json` so
   the next translator run cannot write the old name back."
3. `docs/modding/module-map.md` 430-432: "pass; edit its `NAMES` dict first." becomes "pass; edit its
   `NAMES` dict, read the dry run, then `--apply`. It writes only the ids whose English changed and removes
   them from `tools/translation_cache/<lang>.json`, which the translator would otherwise use to write the
   old wording back."; 444-445 ("Takes effect: next save load for ... and for display names; new campaign
   only ...") becomes "Takes effect: next save load for `posX`/`posY`/`gate_pos*` and the English display
   name; the 12 translated names at the next game start or after a language switch
   (`MBTextManager.ChangeLanguage`, v1.4.8); new campaign only for `owner`, `prosperity`, `hearth` and
   `<Building level>`".
Leave `docs/modding/file-catalogue.md` (its af2b198e4 hunks name 1.5.x-only tools and 1.5.x counts).

**Verify**: `python tools/lint_docs.py` shows no new finding in the three files;
`grep -c "evicts their cache rows" docs/reference/taom-map-settlement-naming.md` prints 1.
Commit: `docs(map): v2.0.28 - map naming docs describe the safer renamer`. Research: the takes-effect
claim (taom-src `TaleWorlds.Localization.MBTextManager` 61-68). Ported-from `af2b198e4`, `244c15d27`, `87ffea9b2`.

### Part B done criteria

- [ ] 3 more commits (11 in all, 10 if A1 was Case L); the 26 new Part B tests pass; tool failure list has no new entry.
- [ ] `git diff bannerlord-1.4.5 -- tools/Apply-MapVillageNames.py | grep -c '^-    "'` prints 0 (no NAMES row removed).
- [ ] Install snapshot unchanged (see "Done criteria").

---

## Part C: the RDC gate and the tpac doc corrections (2 commits, P3)

### Step C1: `check_rdc_entries.py` lists packages the client will skip (4f7ae42ef)

1. RED: create `tools/tests/test_check_rdc_entries.py` (new work, 5 tests):
   ```python
   #!/usr/bin/env python3
   """tools/check_rdc_entries.py on a synthetic module tree (#616).

   Run:  python -m unittest tools.tests.test_check_rdc_entries
   """
   import contextlib
   import io
   import os
   import sys
   import tempfile
   import unittest
   import uuid
   from pathlib import Path

   sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

   import check_rdc_entries as cre  # noqa: E402

   GUID = uuid.UUID("12345678-1234-5678-1234-567812345678")


   def tpac(path, guid=GUID):
       path.parent.mkdir(parents=True, exist_ok=True)
       path.write_bytes(b"TPAC" + (2).to_bytes(4, "little") + guid.bytes_le + b"\0" * 12)


   class CheckRdcEntriesTests(unittest.TestCase):
       def setUp(self):
           self._tmp = tempfile.TemporaryDirectory()
           self.addCleanup(self._tmp.cleanup)
           self.module = Path(self._tmp.name)

       def run_main(self, *argv):
           out = io.StringIO()
           with contextlib.redirect_stdout(out):
               code = cre.main(["--module", str(self.module), *argv])
           return code, out.getvalue()

       def test_no_runtime_data_cache_folder_is_a_failure(self):
           tpac(self.module / "Assets" / "creature" / "x_geo.tpac")
           code, out = self.run_main()
           self.assertEqual(1, code)
           self.assertIn("no RuntimeDataCache folder", out)

       def test_a_package_with_its_entry_passes(self):
           tpac(self.module / "Assets" / "creature" / "x_geo.tpac")
           (self.module / "RuntimeDataCache").mkdir()
           (self.module / "RuntimeDataCache" / f"{GUID}.rdc").write_bytes(b"")
           code, out = self.run_main()
           self.assertEqual(0, code)
           self.assertIn("without-rdc=0", out)

       def test_a_package_without_its_entry_is_listed_and_an_rtemp_does_not_count(self):
           tpac(self.module / "Assets" / "creature" / "x_geo.tpac")
           (self.module / "RuntimeDataCache").mkdir()
           (self.module / "RuntimeDataCache" / f"{GUID}.rdc.rtemp").write_bytes(b"")
           code, out = self.run_main()
           self.assertEqual(1, code)
           self.assertIn("NO RDC", out)
           self.assertIn("rtemp pending", out)

       def test_materials_are_not_checked_by_default(self):
           tpac(self.module / "Assets" / "x_mtl.tpac")
           (self.module / "RuntimeDataCache").mkdir()
           code, out = self.run_main()
           self.assertEqual(1, code)  # a gate that checked nothing is not a pass
           self.assertIn("packages=0", out)

       def test_a_skeletal_animation_master_without_an_entry_is_not_a_failure(self):
           # The Kit writes no .rdc for a master; measured 2026-09-18 (warg 56/56, elephant 31/31).
           master = self.module / "Assets" / "creature" / "x_anm.tpac"
           tpac(master)
           with open(master, "ab") as fh:
               fh.write(cre.SKELETAL_ANIMATION_TYPE)
           (self.module / "RuntimeDataCache").mkdir()
           code, out = self.run_main()
           self.assertEqual(0, code)
           self.assertIn("animation-masters-without-entry=1", out)


   if __name__ == "__main__":
       unittest.main()
   ```
   Run: ImportError (red).
2. GREEN: `git show e45f6df2c:tools/check_rdc_entries.py > tools/check_rdc_entries.py` (stdlib only;
   read-only). This is the 1.5.x end state, not the 4f7ae42ef first cut: `4520af802` stopped it failing on
   skeletal-animation masters, which never have an entry, and made a run that checked nothing exit 1. It
   walks loose `Assets/**/*.tpac`; on this laptop's cooked installs it reports "no RuntimeDataCache
   folder", so it is a desktop gate. Do not take the rest of 4f7ae42ef (the cave troll pipeline; `tpac_fix_item_checksums.py`, which imports
   `tpac_clone_metamesh.py` and needs the `xxhash` package this laptop lacks).

**Verify**: `python -m unittest tools.tests.test_check_rdc_entries` prints `Ran 5 tests` and `OK`.
Commit: `feat(tools): v2.0.28 - check_rdc_entries lists packages the client skips`. Body: the client
renders only packages with a `RuntimeDataCache/<package GUID>.rdc`, which only the Kit writes
(`native-commit-audit-2026-08.md:146`, `:259`); the skip itself was measured on v1.5.3 (#616).
Not-tested: a run against the desktop's loose Armory. Ported-from `4f7ae42ef`, `4520af802` (this tool's hunk only).

### Step C2: tpac checksum formula, the deprecated extract's real faults, and MCP project keys (426f0dc88, 72cad16b1, 0daece880)

1. `git diff 426f0dc88^ 426f0dc88 -- docs/reference/lotrlome-warg-changes.md > ../141-scratch/c2.patch && git apply -3 ../141-scratch/c2.patch`
   (clean: the file equals the parent). Then in the new blockquote replace these two lines
   ```
   > payload; `tools/tpac_clone_metamesh.py` recomputes both and its tests pin the formulas against the
   > live spider bundle. A metadata insert from files is therefore possible now. The Owner Skeleton
   ```
   with
   ```
   > payload; both were measured on every item and segment of the live spider bundle on the 1.5.x
   > line, whose `tools/tpac_clone_metamesh.py` recomputes them (plan 144 ports that tool to this
   > line, without its tests). A metadata insert from files is therefore possible now. The Owner Skeleton
   ```
2. `docs/ai-includes/creature-mount-authoring.md` line 151 (row 7): replace "**It cannot be patched from
   files:** adding a binding grows the item's metadata, so it is an insert rather than a same-length guid
   swap." with "**Patching it from files needs the checksum recomputed:** adding a binding grows the item's
   metadata, and the item checksum is xxHash64 (seed 0) over the int64 metadata length plus the metadata
   (known since 2026-09-17, #616, measured on the 1.5.x line); the 2026-08-29 warg fix predates that and
   was made in the Kit." In item 17 (458-466) replace "a standalone skeleton tpac is an unproven" with
   "a standalone skeleton tpac built by that tool is an unproven" and "structure, and every shipping
   creature" with "structure on the client, and every shipping creature".
3. `docs/tools/spider-skeleton-tpac-tools.md` line 33: before "The tool now refuses to run without" insert
   "Its header carried two faults, both visible in the tool source: the skeleton's item GUID reused as
   the package GUID, and a TOC size of 0 in bytes 28 to 35, from which the engine derives the data start
   ([lotrlome-warg-changes.md](../reference/lotrlome-warg-changes.md) section 13). " (no `DsAssetPackages`
   or server text: D7).
4. `docs/reference/mcp-servers.md`: after the paragraph at line 65 ("Project-level MCP servers ...") add a
   blank line and:
   ```
   **Project servers are keyed by the folder's spelling.** `~/.claude.json` keeps a project's local
   servers under the path as it was opened, case included: on 2026-10-16 `blender` and
   `substance-painter` sat under `E:/repos/TAOM`, and a session opened as `e:\repos\TAOM` read the
   lowercase key, which held none, so neither loaded (measured on the desktop). Open a checkout with the
   same spelling every time.
   ```

**Verify**: `python tools/lint_docs.py` shows no new finding in the four files (the new link in item 3
resolves); `grep -c "xxHash64" docs/reference/lotrlome-warg-changes.md` prints 1 or more.
Commit: `docs(tools): v2.0.28 - tpac checksum formula, extract faults, MCP keys`. Research: the extract
header read from `tools/tpac_skeleton_extract.py`; the TOC-size fact at `lotrlome-warg-changes.md` section 13.
Ported-from `426f0dc88`, `72cad16b1`, `0daece880`.

### Part C done criteria

- [ ] 2 more commits (13 in all, 12 if A1 was Case L); 5 new tests pass; tool failure list has no new entry; no new lint finding.

---

## Part D: the graphify wrapper and gate script, unwired (P3, optional)

graphify is a desktop tool here: the laptop has neither graphify nor an `E:` drive, so the wrapper will
exit 3 on the laptop until the maintainer installs graphify and sets `TAOM_GRAPHIFY_OUT`. This Part lands
the wrapper, its hermetic tests, the gate script and the feature doc. The workflow wiring (skills,
agents, the SessionStart freshness line, ADR-012 and the ADR-010 amendment) is owed and not ported in
this programme: WP143 Part G reports it as owed and takes none of it (see "Hand-offs").

**What the gate does with no wiring** (read at `e45f6df2c` and `e6745a492`). Unregistered (before D2, or
if the maintainer declines D2) nothing calls the script, so it is inert. Registered, it reads no skill,
agent, SessionStart line or ADR: a payload with neither the text `graphify` nor a `\u` escape returns
`{}` at the prefilter, before Python starts; anything else goes to `tools/graphify_taom.py gate`, which
D1 lands. That judge denies the raw write verbs, allows the query verbs and commands that only mention
graphify as data (a commit message, a grep pattern), and its deny reason points only at the wrapper and
`docs/features/graphify-code-graph.md`, both also from D1. A missing judge or no safe Python fails open
with a stderr line. On the laptop, where graphify is absent, a deny only stops a command that could not
run anyway. It does not misfire without the wiring, so the hook stays in this plan.

### Step D1: wrapper, tests, gate, doc (b8833474e, e6745a492)

1. RED: `git show e6745a492:tools/tests/test_graphify_taom.py > tools/tests/test_graphify_taom.py` (82
   tests, temp git repos only). Run: ImportError.
2. GREEN: `git show e6745a492:tools/graphify_taom.py > tools/graphify_taom.py`. The module test prints
   `Ran 82 tests` and `OK`.
3. Gate: `MSYS_NO_PATHCONV=1 git show e45f6df2c:.claude/hooks/check-graphify-usage.sh > .claude/hooks/check-graphify-usage.sh`
   (`e45f6df2c` differs from `b8833474e` by one comment line). `bash -n .claude/hooks/check-graphify-usage.sh` exits 0. Smoke:
   `printf '%s' '{"tool_name":"Bash","tool_input":{"command":"graphify update x"},"hook_event_name":"PreToolUse"}' | bash .claude/hooks/check-graphify-usage.sh`
   prints a line containing `"permissionDecision":"deny"`; with `graphify god-nodes --top 5` it prints `{}`.
4. `tools/test_hooks.sh`: immediately above the `# ----` line that precedes `head2 "Summary"`, add:
   ```bash
   # ---------------------------------------------------------------------------
   head2 "7-graphify. check-graphify-usage denies raw graphify writes and allows queries (#677)"
   # The judge's full case table is tools/tests/test_graphify_taom.py; these rows prove the hook end
   # to end: the prefilter, the judge's path relative to the hook, the nested decision, both shell tools.
   GU_CASES=(
     "Bash|deny|graphify update E:/graphify/TAOM"
     "Bash|deny|cd /x"$'\n'"graphify extract E:/repos/TAOM --code-only"
     "Bash|deny|graphify claude install"
     "Bash|deny|bash -c \"graphify update x\""
     "Bash|allow|graphify affected \"IModLogger\" --depth 2 --graph g.json"
     "Bash|allow|python tools/graphify_taom.py refresh --if-stale"
     "Bash|allow|git commit -m \"docs: graphify update drops external nodes\""
     "PowerShell|deny|& \"C:\\Users\\mikew\\.local\\bin\\graphify.exe\" update x"
     "PowerShell|deny|graphify extract E:\\repos\\TAOM --code-only 2>&1 | Select-Object -Last 5"
     "PowerShell|allow|graphify god-nodes --top 15 --graph E:\\graphify\\TAOM\\graphify-out\\graph.json"
   )
   for entry in "${GU_CASES[@]}"; do
       tool="${entry%%|*}"; rest="${entry#*|}"; want="${rest%%|*}"; cmd="${rest#*|}"; shown="${cmd//$'\n'/\\n}"
       payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PreToolUse"}))' "$tool" "$cmd")
       OUT=$(printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash .claude/hooks/check-graphify-usage.sh 2>/dev/null)
       got=$(printf '%s' "$OUT" | "$HPY" -c 'import json,sys
   t = sys.stdin.read().strip()
   d = json.loads(t) if t else {}
   h = d.get("hookSpecificOutput") or {}
   print(h.get("permissionDecision") or d.get("permissionDecision") or "allow")' | tr -d '\r')
       [[ "$got" == "$want" ]] && ok "check-graphify-usage [$tool] $got for: $shown" \
           || bad "check-graphify-usage [$tool] expected $want, got $got for: $shown"
   done
   ```
   If the file has the prefilter sections from plan 013/027 (`grep -n 'PF_NARROWED_LIST' tools/test_hooks.sh`
   prints a line), also apply b8833474e's rows there by content: add `check-graphify-usage.sh` to
   `PF_NARROWED_LIST`; add the trigger row
   `check-graphify-usage.sh) triggers=('cd /x\ngraphify update /y' 'echo a; graphify extract .') ;;`
   and the escape row `check-graphify-usage.sh) esc="cd /x\n${PF_U}0067raphify update /y" ;;` beside the
   `block-no-verify.sh` rows; and the 4d row
   `"check-graphify-usage.sh|$SANDBOX|rc=0 deny|graphify update x|${PF_U}0067raphify update x"`.
   In every case append ` && graphify update x` to the command of section 5's starved-environment payload
   (`git push --force origin master...`), so the gate reaches its degraded branch there.
5. Feature doc: `git show e6745a492:docs/features/graphify-code-graph.md > docs/features/graphify-code-graph.md`, then:
   - replace the ADR-012 markdown link (`[ADR-012](../adrs/012-graphify-code-graph-in-the-workflow.md)`)
     with plain `ADR-012 (1.5.x line)`;
   - after the Overview section's first paragraph add:
     ```
     **On the 1.4.x line.** The wrapper, its tests and the gate script are here; the gate runs once the
     maintainer registers it in `.claude/settings.json` (plan 141, step D2). The workflow wiring this page
     describes (the `/deep-review`, `/investigate`, `/new-feature` and `/research` steps, the agent lines,
     the SessionStart freshness line and ADR-012) is not on this line yet. graphify is not installed on
     the maintainer's laptop, where every command exits 3 until it is and `TAOM_GRAPHIFY_OUT` names a
     local folder; the default output root is the desktop's `E:\graphify\TAOM`.
     ```
   - prefix the "Where the workflow reads it" table with the sentence `On the 1.5.x line:`;
   - delete the auto-generated footer from `<!-- backlinks-start` through `<!-- backlinks-end -->`.
6. `docs/reference/provenance-register.md`: in the table row starting `| graphify |`, replace
   "`tools/doc_graph.py` `tools/graph_query.py` |" with "`tools/doc_graph.py` `tools/graph_query.py`;
   `tools/graphify_taom.py` runs the CLI (interop-only, see detail) |"; replace the two paragraphs from
   "**It remains installed on the maintainer's machine and wired into nothing:**" through
   "...why it is deliberately absent from CLAUDE.md:" and its link line with:
   ```
   **On this line since plan 141 (#677):** `tools/graphify_taom.py` runs the installed CLI as a subprocess
   and `.claude/hooks/check-graphify-usage.sh` judges its raw write verbs; the wrapper is `interop-only`:
   it copies no graphify code, and graphify's `cache.py` was read only to find where its stat-index cache
   is written. The gate runs once the maintainer registers it; the workflow wiring (ADR-012 on the 1.5.x
   line) is not here yet. Still no MCP registration, no CI job and no `graphify * install` subcommand.
   Nothing from graphify ships in a TAOM release, so the license position above is unchanged. Remove
   with `uv tool uninstall graphifyy`; the wrapper then exits 3.

   The derivation of `tools/doc_graph.py` stays `behavioural-port`. Usage:
   [`docs/features/graphify-code-graph.md`](../features/graphify-code-graph.md); the trial's measurements:
   [`docs/reviews/adopt-graphify-v8-2026-08-18.md`](../reviews/adopt-graphify-v8-2026-08-18.md).
   ```
7. `.gitignore` 143-147: replace the five comment lines above `graphify-out/` with:
   ```
   # graphify output. `graphify extract --out` defaults to the SCANNED directory, and even
   # with --out an incremental re-run writes graphify-out/cache/stat-index.json into the
   # scanned tree unless GRAPHIFY_OUT is absolute (2026-09-26). That already happened once:
   # Main/graphify-out/cache/stat-index.json, 684 KB, was committed via 8318e346 on the 1.5.x
   # line. Build the graph only with tools/graphify_taom.py, whose contamination check refuses
   # to stamp a build that leaked. Never repo content.
   ```

**Verify**: `python -m unittest tools.tests.test_graphify_taom` OK (82); `bash tools/test_hooks.sh --verbose 2>&1 | grep -c "check-graphify-usage \["`
prints 10 and `bash tools/test_hooks.sh` has no FAIL line the Step 0 file lacks; `python tools/lint_docs.py` shows no new finding
in the two docs (the feature doc is linked from the provenance register, so it is not an orphan).
Stage the seven files. Commit: `feat(tooling): v2.0.28 - graphify wrapper and gate script (unwired)`.
Not-tested: the gate live in a session (needs D2). Ported-from `b8833474e`, `e6745a492`.

### Step D2 (maintainer applies; executor verifies and commits): register the gate

1. Write this script to `C:/Users/mikew/source/repos/taom-port/141-scratch/settings_graphify.py`. Do NOT run it.
   ```python
   """WP141 D2: register check-graphify-usage.sh beside validate-push.sh (config-protection keeps agents
   out of .claude/settings.json). Run from the WP141 worktree root. Idempotent."""
   import json
   import pathlib

   p = pathlib.Path(".claude/settings.json")
   d = json.loads(p.read_text(encoding="utf-8"))
   groups = [g for g in d["hooks"]["PreToolUse"] if any(h["command"].endswith("/validate-push.sh") for h in g["hooks"])]
   assert len(groups) == 1 and groups[0].get("matcher") == "Bash|PowerShell", "expected WP103's Bash|PowerShell git-gate group"
   group = groups[0]
   push = next(h for h in group["hooks"] if h["command"].endswith("/validate-push.sh"))
   if not any(h["command"].endswith("/check-graphify-usage.sh") for h in group["hooks"]):
       group["hooks"].append({"type": "command",
                              "command": push["command"].replace("validate-push.sh", "check-graphify-usage.sh"),
                              "timeout": 10})
   p.write_text(json.dumps(d, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
   print("settings.json updated")
   ```
   The exact change it makes, inside the PreToolUse `Bash|PowerShell` group that holds `validate-push.sh`,
   after that group's last hook:
   ```diff
                "timeout": <unchanged>
   +          },
   +          {
   +            "type": "command",
   +            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/check-graphify-usage.sh",
   +            "timeout": 10
              }
   ```
2. STOP and ask the maintainer: "WP141 D2: please review
   `C:/Users/mikew/source/repos/taom-port/141-scratch/settings_graphify.py`, then run
   `cd C:/Users/mikew/source/repos/taom-port/141 && python ../141-scratch/settings_graphify.py && git diff --stat -- .claude/settings.json`
   (expect one file, about 5 insertions), and tell me when it is done." If the maintainer declines,
   skip D2 and record it in the hand-off.
3. After confirmation: `python -m json.tool .claude/settings.json > /dev/null && echo JSON-OK`;
   `bash tools/test_hooks.sh` has no FAIL the Step 0 file lacks (sections 2 and 7j accept the anchored,
   timed entry). Stage `.claude/settings.json` only. Commit:
   `chore(hooks): v2.0.28 - register the graphify gate for both shell tools`. Ported-from `b8833474e`.
   Delete the script.

### Part D done criteria

- [ ] 1 or 2 more commits (14 or 15 in all, one fewer if A1 was Case L); 82 new tests pass; hooks run has no new FAIL.

---

## Disposition (every commit in the pack, plus the shared hunks)

| SHA (1.5.x) | Subject (short) | Disposition |
|---|---|---|
| `e1a940ac9c2fcaa6dd573b982409ad4e8cf17344` | Brown and Pale Spider mounts (#616) | Skipped: `tpac_clone_metamesh.py` and its tests need the `xxhash` package (absent here, so the test module would add an import error); the spider variants are 1.5.x content. WP144 step B5 brings the tool file later, from `991f415f4`, without those tests |
| `426f0dc88f33fd0e0778c455f67d7c81d3b74324` | clone tool recomputes the tpac hashes | Adapted (C2): the xxHash64 formulas into the warg ledger and row 7, without the tool (WP144 step B5 brings it later); lesson to WP142 |
| `5ccde2771850698ccb9be912a4b341e073630f6c` | invisible clones were a missing RDC entry | Skipped here (row 7b absent; RDC premise already at `native-commit-audit-2026-08.md:146,:259`); its rewritten lesson to WP142 |
| `525f67fc9796a953e9faeb9f8ce7dc339f3e960f` | CLAUDE.md trap: a tpac the Kit never saved | Hand-off to WP143 (final wording from 3c5e57e71, naming `check_rdc_entries.py`) |
| `4f7ae42ef14e18d646f0160f748c10e580c1f019` | Fab cave troll clip pipeline | Partial (C1): `check_rdc_entries.py` only. Skipped: the cave troll pipeline (1.5.x content; `bind_troll_action_set.py` writes the live Armory) and `tpac_fix_item_checksums.py` (imports the clone tool, needs `xxhash`) |
| `3c5e57e7193bf21339e93f5aba1b2e0bceba00c6` | cave troll clips: Kit facts | Skipped: the Kit facts were measured with the 1.5.x line's Kit (UNVERIFIED on the v1.4.8 Kit) and are written around the troll tools left on 1.5.x; RDC trap row to WP143; the engine-skeleton CLAUDE.md row is WP144's (from 4520af802) |
| `8465cc9843601b7ac1c885d9116629c1c186700f` | #617 review: translations, ladder rules | Partial (A6): atomic `write_changes`, militia quote regex, their tests. Rest builds on #617 (absent) |
| `e63128528ffe61a961bad52f9b153ab1bfb69cc8` | FBX LOD tools (mislabelled) | Skipped (deferred): the 1.4.8 Armory has no LOD pass (maintainer's art decision; needs FBX AssetSources and a Kit re-import, the laptop is cooked only); the tools were reworked later on 1.5.x (30abf550b, f1675c058) |
| `b8833474ee24856fd5ecd1b073cd20f648e80f69` | wire graphify into the workflow | Partial (D1, D2): wrapper, tests, gate, doc, provenance, .gitignore; registration by the maintainer. The workflow wiring, ADR-012 and the ADR-010 amendment are owed and not ported in this programme (see "Hand-offs") |
| `a8fc5c848d8b43bb8efba7264f96ea13b023f50a` | creature fit tools (mislabelled) | Skipped: serves the 1.5.x hill troll fit; its docs were verified on v1.5.3 only and three it edits are absent here; no creature authoring is planned on this line |
| `44e9edd48bfa5d3db85efe5f821cfd71341a3f9b` | Harad pools, kits and names | Partial (A4, A5): seed marker, `mistymountainorcs` pick. Harad data, pools, kits, names and the abanissa alias are 1.5.x rework content |
| `a9f2d7edab4096dee8cf2524949686d0be0bb560` | safer port tools | Partial (A1, or WP135 step A7 when A1 is Case L; A2, A4). Skipped: Shaghana party templates (content), `register_armory_names.py` and `merge_live_file.py` (1.5.x artist-drop workflow; a 1.4.x baseline would freeze a pinned Armory), `--reseed` and the Harad bow table, melee-ladder and armour-class hunks (passes absent here), generator Harad armour names |
| `71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42` | painter scripts sweep (mislabelled) | Partial (A1, or WP135 step A7 when A1 is Case L; A2, A4, A5, A8 end state). Skipped: Painter/Blender scripts, Carn Dum icon, Erebor and Harad content, `DwarfDressWearerTests`, melee pricing, `HaradWeaponsLiveEditTests`, the prep-tripo-prop skill and CLAUDE.md row; localize skill line and lessons to WP143 and WP142 |
| `041808e5fd13b0db0d729e178b6922a040ec98f3` | map icon tooling review fixes | Partial (A8): converter end state and test. Skipped: `blender_job.py` and the prep-script change (needs `kitbash_kits.py`, absent; no poller here), Painter, unwrap and builder scripts |
| `0daece880b1e2bf174c5878e165868d9d4826700` | Carn Dum RCA, lessons, workflow docs | Partial (C2): the MCP folder-spelling note; generic lessons to WP142; rest is map-icon content |
| `a69cd39df7c1aba6f698c74c810c6b73f09447f0` | prep-tripo-prop covers the rebuild route | Skipped: the skill exists only on 1.5.x (71cc4c6d2 is SKIP-CONTENT) and the map-icon pipeline is not ported |
| `b75fbab076e60e0e7c8352eb418123dad65c9177` | clan heraldry keeps line endings (#930) | Ported (A7), README hunk to WP142 |
| `798e4d0bf8b4a1aa9f40fb6e15f0e97725a59264` | generators leave the mirror alone | Adapted (A3): re-implemented without `_gamedir.armory_trees` |
| `72cad16b105d4f752558ac1b7f004b2cfc92e285` | dedicated server page, stale docs | Partial (A3 mirror wording, C2 extract reason). Skipped: co-op server page and all server text (D7), armory-ref-audit, INDEX/feature-map/doc-lookup rows |
| `e6745a49208ae2b9bb7223a501aec41bfe2b1d9b` | graph finds interface and member callers (#995) | Partial (D1): wrapper and tests at this state, doc. Skipped: the detached SessionStart refresh (needs 407570ece's probe budget, not on this line); agent, skill and `.agents` lines are owed and not ported in this programme (see "Hand-offs"); edits to files absent here |
| `244c15d2779dec00f2e80d4e23a98387448e49df` | one-off scripts (mislabelled) | Partial (B1, B2, B3). Skipped: the two one-off scripts, cache deletions, `add_map_fortifications.py`, name-tag tests, Lindon docs |
| `87ffea9b2fbc0d96f2e43b9173d2f8d8a22ab28b` | Dunland names, safer tools | Partial (B1, B2, B3). Skipped: one-offs, retirement, caches, Lindon docs, the release-process repair check (needs the unported one-off). The Dunland name repair is live content (maintainer) |
| `af2b198e45432233e696ce17c6898fe73101c9b1` | map docs match the tools | Partial (B2 docstrings, B3 docs). Skipped: Lindon counts, release notes, RCAs, file-catalogue (1.5.x tools and counts), name-grammar-tags |
| `4b207f951b373e4182e0b4a1ee4507b0d61b3523` (shared, K2 F6) | Dorwinion and South Rhovanion | Partial (B1): `master_attr`, `master_village_bound`, the two `check()` findings, help and OK text, 3 tests. No `Village` rows, no `MESHES` lines |
| `b904e02a3625d5a81308e0880a85b71912232c9b` (shared, K2 F9) | #617 second review | Adapted (A6): the civilian-set skip, inlined. Rest skipped (1.5.x ladder rework; templated-troop premise false on v1.4.8) |
| `cabfe3a99880f018e403781edb26e4794ba74709` | Spider Rider line | Skipped: content |
| `0919ae06408e4fae6555c1fe1c60dec51c9af118` | rider line levels | Skipped: content (balance) |
| `8800aa4cc1b610de4ee52895d3e105ee659d9dc6` | #617 review RCA, lessons | Skipped: bookkeeping for the 1.5.x-only #617 |
| `449d80a837ab84e8fece6e0e62bf3b3e6ef103e2` | #617 second review records | Skipped: bookkeeping |
| `caefaa1f3943d053be6539e2cb5166197db94042` | Harad repair RCA and lessons | Skipped: bookkeeping (lessons and an RCA for the 1.5.x Harad data repair) |
| `71cc4c6d2266f6f13a53d27fa008e5e43ea297d4` (prerequisite) | prep-tripo-prop skill | Skipped with a69cd39df |
| `4520af802` (WP144's group; shared hunk) | ram hits one foe, elephant hit capsules | Partial (C1): `tools/check_rdc_entries.py` at its end state only; the rest is WP144's |
| `407570ecea85fb22762075eb1ff5c665830afe2e` (prerequisite) | upstream check, probe budget | Not needed: the SessionStart probe is not ported (WP103 owns the rest) |

## Hand-offs (for the integrator to pass on)

**WP142 (docs-process sync), after this WP merges:**
- `tools/README.md` rows for: `generate_starter_kit.py` (foreign-row guard, all or nothing, exit codes,
  mirror only with `--asset-repo`, `--no-asset-repo` the default), `generate_ranged_ladder_items.py`
  (mirror only with `--asset-repo`), `translate_with_claude.py` (`--max-entries 0`; `--seed-only` in Case
  N or L), `oneoff/generate_tavern_mercenaries.py` (dry run unless `--apply`), `generate_enlistment_rosters.py`
  (seed marker), `check_generator_item_refs.py` (fallback table registered), `fix_upgrade_armour_regressions.py`
  (parses every file first), `generate_clan_heraldry.py` (b75fbab07's README hunk, including the
  `read_bytes` sample), `oneoff/convert_tripo_prop_textures.py`, `Apply-MapVillageNames.py` and
  `add_map_villages.py` (244c15d27, 87ffea9b2 rows without name tags), `tpac_skeleton_extract.py`
  (72cad16b1 reason without the server sentence), `check_rdc_entries.py` (new), `graphify_taom.py` (new, if Part D).
- Lessons: `build-tooling-workflow.md`: 71fea53c8 "Stage a file two sessions append to by hunk...",
  "A guard that refuses must run before the first write...", "A cap of zero must be a cap, most of all on
  paid calls"; 0daece880 "A tool's default output never points into a live module", "A step written for
  one item refuses many", "Label an estimate as an estimate, with its inputs"; 87ffea9b2 "Write last the
  file a re-run reads to decide what is left". `animation-skeleton.md`: 5ccde2771 "A hand-built tpac
  package is invisible to the game until the Modding Kit has cooked its RuntimeDataCache entry" (without
  pointing at the clone tool). `localization-ui.md`: af2b198e4 "A rename must evict the translation cache...".
- `docs/INDEX.md` and `docs/reference/feature-map.md` rows for `docs/features/graphify-code-graph.md` (Part D).

**WP143 (harness-rules sync):**
- `CLAUDE.md` Traps row "A tpac the Kit never saved is invisible, with no log line" (3c5e57e71 wording,
  `Gate: python tools/check_rdc_entries.py --under <folder> (#616)`), within 400 characters and the
  CLAUDE.md byte budget; only if Part C landed.
- `.claude/skills/localize/SKILL.md` step 3: the sentence "When translation is deferred,
  `--sync-ids --seed-only --apply` seeds the rows without the paid translate phase." (Case N or L).

**WP144 (docs-engine sync):** nothing owed. 3c5e57e71's Kit facts stay out until a v1.4.8 Kit re-measure.

**Owed, not ported in this programme (no WP takes it; WP143 Part G reports it as owed):**
- graphify wiring (b8833474e, e6745a492): `/deep-review` Step 1, `/investigate` 2a, `/new-feature`,
  `/research`, the feature-builder agent line, `.agents/skills` taom-build, taom-research and taom-review
  lines, the AGENTS.md line, the agent-operating-manual row, the evidence-over-claims pointer, a bounded
  SessionStart freshness line, ADR-012 and the ADR-010 amendment (maintainer edits). Part D's gate does
  not need any of it (see "What the gate does with no wiring").

## Test plan

- New tool tests (Case N): A1 6, A2 17, A3 3, A4 9, A5 3, A6 5 (4 red first, 1 guard), A7 7, A8 14, B1 13,
  B2 13, C1 5, D1 82: 177 in all (Case W: 174; Case L: 171, A1's 6 having come with WP135). Structural patterns: the copied 1.5.x modules; new files
  model `tools/tests/test_add_map_villages.py` (pure stdlib over synthetic text).
- C#: no new test; `LanguageFileCoverageTests` message changes (Case N only) and must stay green.
- Untestable here: the tools' real runs on the desktop (`--apply` against the live Armory, TAOM_Map, the
  paid translator), the gate live in a Claude session. Name them in `Not-tested:`.
- Full verification at the end: all tool tests, Build, full Test, hooks, docs lint.

## Done criteria (whole plan)

- [ ] 13 commits for Parts A to C, 12 if A1 was Case L (plus 1 or 2 for Part D); every subject matches
      `^[a-z]+(\([a-z-]+\))?: v2\.0\.28 - ` and is at most 72 characters
      (`git log --format=%s bannerlord-1.4.5..HEAD | awk 'length > 72'` prints nothing).
- [ ] `git log --format=%B bannerlord-1.4.5..HEAD | grep -c "Co-Authored-By\|Generated with"` prints 0, and
      a dash scan of the bodies (`grep -c $'\xe2\x80\x94\|\xe2\x80\x93'`) prints 0.
- [ ] The tool failure list has no entry the Step 0 file lacks, and the new tests above pass.
- [ ] Build exit 0; Test 0 failed with the Step 0 total.
- [ ] `bash tools/test_hooks.sh`: no FAIL line the Step 0 file lacks.
- [ ] `python tools/lint_docs.py`: no new finding in any touched doc.
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD` lists only in-scope files; `git status --porcelain` is empty.
- [ ] Install snapshot: re-run the Step 0 `stat` into `../141-scratch/install-after.txt`;
      `diff ../141-scratch/install-before.txt ../141-scratch/install-after.txt` prints nothing.
- [ ] `git diff bannerlord-1.4.5..HEAD -- Main/_Module CHANGELOG.md` prints nothing.

## STOP conditions

Stop and report (do not improvise) if:
- A Step 0 precondition fails (WP102, WP103 or, unless waived, WP137 not merged).
- A file's lines a step touches differ from "Current state" for a reason the "Overlaps" list does not
  explain (for example `troop_weapon_classes` or `MILITIA_BINDING_RE` already changed).
- A1 finds `--seed-only` with semantics other than the three cases (for example an argparse error when both
  modes would register the flag).
- `git apply -3` leaves a conflict in A2 that is not one of the three 1.4.x-only spots named there.
- A5's findings command prints a `tools/generate_enlistment_rosters.py` line (a fallback-kit id the installed
  Armory lacks): that is a data defect; record the ids and stop, never edit the data. Findings for other
  generators are the pre-existing state: carry on.
- A4's tests pass but `git status` shows `Main/` changes (the tool ran against real data).
- B2's NAMES check prints anything but `438 438 True`, or `tools/translation_cache` shows a change.
- The install snapshot differs at any point: stop immediately and report which file.
- A tool test needs a package this laptop lacks (only numpy, Pillow and lz4 are expected), or a test
  reaches the network.
- Part D: the gate smoke does not deny a raw `graphify update`, or D2's script assertion fails (WP103's
  `Bash|PowerShell` group is missing).
- Any step's verification fails twice after a reasonable fix attempt.

## Maintainer steps

- **M1 (optional, Part D2)**: review and run `settings_graphify.py` (exact script in D2). Without it the
  gate is present but inert.
- **M2 (content decision, not this plan)**: the 1.4.8 `TAOM_Map` package carries `Epicrotea` for
  `town_EN1` (master says `Galtrev`) in BR, DE, FR, IT, SP and TR; 1.5.x repaired its live map with
  one-off tools this plan does not port. Decide whether the pinned 1.4.8 package gets the same repair.
- **M3 (art decision)**: whether the 1.4.8 Armory gets the distance-LOD pass; if yes, port e63128528's
  tools from their 1.5.x end state then.

## In-game checks owed

None for gameplay: this plan changes developer tools, one C# test message and docs; no file players
load changes. Owed instead, on the desktop, the first real use of each changed tool (each now dry by
default, so read the dry run first): `Apply-MapVillageNames.py --apply` after a rename (then a DE
client shows the new English name until a translator run; switch language to see it refresh), the
starter kit and ranged ladder generators without `--asset-repo` (the mirror must stay untouched), and,
after D2, a raw `graphify update` from both shell tools must be denied in a live session.

## Maintenance notes

- Newer 1.5.x commits outside this programme touch these tools: `c30c4e618` and `ad8f13d00` (the village
  tool refuses a scene with no `scene.xscene` and checks a live row's scenes; worth porting to this line
  later), `44d4dba46` (translator seeding style). Classify them in a later pass.
- Lead, UNVERIFIED on v1.4.8: this line's `add_map_villages.py` inserts new master rows after an anchor,
  while 1.5.x appends them at the end of the file (`append_settlements`) so a save made before a batch
  does not drop the rows after the new one. v1.4.8 unregisters non-ready objects on load
  (`Campaign.cs:1432-1438`); whether a new row's alley init throws on an old save here is not checked.
  Open an issue before the next village batch on this line.
- `rebalance_troops.troop_weapon_classes` now differs from 1.5.x in one deliberate way: it keeps an
  `<equipment>` written straight under `<Equipments>` (v1.4.8 applies it to every set), which 1.5.x's
  `rl.battle_sets` drops. Reconcile if the per-tier ladder ever comes to this line.
- WP135 and this plan both touch `--seed-only`; the 1.5.x end state also carries `c2c8afe42`'s TAOM_Map
  refusal and Armory-install check (merged in `b11b6d142`), which belong to WP135.
- A reviewer should probe: A2's 3-way applies (no stray `RETIRED_DONORS`), A4's restored Harad picks,
  B2's removal of the name-tag calls when `tools/name_tags.py` is absent, and that no test reads the
  real install except A5's live gate.
