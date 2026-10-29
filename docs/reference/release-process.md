# Release Process — module versions and git tags

> How a TAOM build gets a version, why that version must be a git tag, and how to turn a player's
> crash report back into a commit. Skill: `/release`. Guard: `.claude/hooks/check-version-tagged.sh`.

## Why this exists

The version a player sees comes from one place: `<Version value="v2.0.18" />` in
[`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml). At runtime
[`IdentityCollector`](../../Main/Features/CrashReport/Collectors/IdentityCollector.cs) reads it via
`ModuleHelper.GetModuleInfo("TAOM")?.Version` and stamps it into every crash bundle as
`TaomVersion`. When a player reports a CTD, that string is the link between their report and our
source (bundles written since plan 017 also carry the build stamp, which names the commit; see
"Resolving a crash report to a commit" below).

Until 2026-08-08 that link went nowhere. The repo had two tags, neither a release
(`crafting-tool-v1.0`, `archive/master-pre-1.4.5-promotion`), and `git describe` read
`crafting-tool-v1.0-492-gd9817f89`. Worse, five versions players ran were never committed at all —
including `v2.0.12`, which appears in two crash reports in [`CHANGELOG-2026-H2-handwritten.md`](../changelog-archive/CHANGELOG-2026-H2-handwritten.md)
(a Rhûn notable CTD and the Nan Angren deserters CTD). Those reports cannot be pinned to a commit,
or even to a range, because git has never seen the version they name.

## The contract

**`Main/_Module/SubModule.xml`'s `<Version>` changes only in a release commit, and that commit is
tagged `vX.Y.Z` and pushed immediately.**

Two consequences, both load-bearing:

- A version string in a crash report is a lookup key. `git show v2.0.18:<path>` reconstructs
  exactly what shipped.
- A build whose version was never committed cannot go out — that is the `v2.0.12` failure.

Tag names are plain `v2.0.19`, matching `SubModule.xml` byte-for-byte. Non-release tags keep their
own namespaces (`archive/…`, `crafting-tool-…`), so `git describe --tags --match 'v[0-9]*'` isolates
releases deterministically.

Tags are **annotated** (`git tag -a`), never lightweight — an annotated tag is a real git object
carrying a tagger, a date and a message, and `git describe` prefers it. **Never move a pushed tag.**
Anyone who already fetched it keeps the old target silently. If a release is wrong, cut a new
version.

## The three version fields

A release bumps up to three fields, and the pairing between the last two is issue #371 — the failure
that shipped bind-posed characters to players:

| File | Field | When |
|------|-------|------|
| [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) | `<Version value="v2.0.X" />` | Every release |
| [`Dependencies/_Module/SubModule.xml`](../../Dependencies/_Module/SubModule.xml) | `<Version value="v2.0.Y" />` | Every release since v2.0.29, so all four modules read one version; it must change whenever the Dependencies assembly changed |
| [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) | `<DependedModuleMetadata id="TAOM.Dependencies" … version="v2.0.Y" />` | **Must equal the line above** |

The third line exists because BUTR/BLSE launchers read `DependedModuleMetadatas` and the vanilla
launcher does not — the in-file comment above it spells this out. A stale pairing lets a new TAOM
load against an old Dependencies, Harmony/UIExtenderEx types fail to resolve at the member level,
and every character renders in bind pose. `/release` asserts the two `v2.0.Y` values match; do not
bump one by hand.

The two unversioned modules carry a version too. The live `<game>/Modules/TAOM_Map/SubModule.xml`
and `<game>/Modules/LOTRLOME_Armory/SubModule.xml` hold their own `<Version>`, outside git, and the
release note tells players all four modules read the release number. Edit both in the install
before Mike's editor package; no commit records the change.

Assembly identity is separate and deliberately static: `Directory.Build.props` freezes
`AssemblyVersion` (changing it alters binding identity for no benefit) and stamps
`InformationalVersion` as `build.yyyyMMdd-HHmmssZ` per build, which both modules log at startup so a
mismatched pair is one line in the log. The .NET SDK appends `+<commit SHA>` to that stamp, and a
build of a tree with uncommitted changes to its inputs appends `.dirty` after the SHA (`nogit` or
`.nogit` when git could not tell). That stamp identifies a *build*; the tag identifies a
*release*. Both are needed.

## Cutting a release

Use `/release`. It runs the sequence below and fails closed on the #371 pairing check.

1. Tree clean (`git status --porcelain` empty; if another session's edits are present, stop, because `build.ps1` compiles and deploys every file in the tree, committed or not; git refuses a second worktree on a branch that is already checked out, so only the Phase 8 build moves to a detached worktree of the tag),
   on the release branch (`bannerlord-1.5.x`, the only release line), current version
   already tagged, and the target version in no tag (`git tag -l 'v*'`) and in no release channel: read the
   `<Version>` of every `E:\LOTRAOM_Releases\<channel>\Modules\TAOM\SubModule.xml`, because a channel can
   hold an untagged build of a number git has never seen (see "A number a channel carried before git
   did" below).
2. `./build.ps1 -RunTests` green — no release on an unrun build.
3. `pwsh tools/sweep_module_backups.ps1` reports 0 files. If it does not, run it with `-Apply`:
   backup sidecars must not ship, because `.bak` breaks the Cloudflare distribution. The first run
   found 781 of them, 937 MB. See [module-backup-sweep](module-backup-sweep.md).
4. Bump the version fields above.
5. Generate the CHANGELOG section: `python tools/changelog_from_commits.py --version vX.Y.Z --write` (every non-merge commit since the previous tag, subject and body verbatim, grouped by type). Its summary names the commit the range ends at.
6. Write `docs/releases/vX.Y.Z-discord.md` from that section (shape: [`v2.0.15-discord.md`](../releases/v2.0.15-discord.md)). Then write the testers' checklist for the testing channel: `python tools/ingame_ledger.py --since <previous tag> --format md` into `docs/releases/vX.Y.Z-testers.md`. It lists every issue closed since the previous tag that still carries `triage-needs-ingame` (#1010). It never blocks a release; a tester's confirmation removes the label.
7. Confirm `HEAD` is still the commit step 5 ended at (if not, restore `CHANGELOG.md` and repeat step 5), then commit `chore(release): vX.Y.Z - TAOM vX.Y.Z`, staging release paths explicitly. Every other
   commit carries the CURRENT version the same way (`<type>: vX.Y.Z - <description>`, user rule
   2026-09-13, hook `check-commit-subject-version.sh`), so between releases
   `git log --grep 'vX.Y.Z - '` lists the commits a build reporting that `TaomVersion` can contain.
8. Confirm `git rev-parse <release commit>^` prints the commit step 5 ended at (if not, stop and ask), then `git tag -a vX.Y.Z <release commit> -m "…"`, tagging the step 7 commit by SHA, then `git push origin <release branch> vX.Y.Z`.
9. Build at the tag and gate the DLLs (the skill's Phase 8). Run `./build.ps1` in the main checkout only when `git status --porcelain` is empty and `HEAD` is the tag's commit; otherwise build a clean worktree of the tag (`git worktree add ../taom-release-vX.Y.Z vX.Y.Z`, `./build.ps1` there, then `git worktree remove ../taom-release-vX.Y.Z`). Then `python tools/package_release.py --source "<game>/Modules" --dest <out> --game-root "<game>" --require-build vX.Y.Z --dry-run` must print `build stamp OK`. Any later deploying build of a dirty tree (`./build.ps1`, or any build without `-p:DisableModuleCopy=true -p:ModuleId=`) replaces the gated DLLs and the module's ModuleData in the install: after the v2.0.34 gate passed, one made the installed `TAOM.dll` `+18a4e402...dirty`. So repeat the dry run right before Mike packages. Mike packages through the Modding Kit editor; Claude never writes a package with this command. The packages and the release folders are his (2026-10-27): unless he asks, Claude does not inspect or gate on `E:\LOTRAOM_Releases\<channel>\Modules`, proves the stamps on the DLLs the tag build deployed (`/release` Phase 8 step 3), and hands him the package checks as a list. Once his package exists in `E:\LOTRAOM_Releases\<channel>\Modules\`, offer the same dry run with `--source` pointing there and `--game-root "<game>"`; it also refuses an untracked TAOM file there (never `AssetPackages/pack0.tpac`, which holds the TAOM UI in a channel folder). ShaderCacheKeeper ships beside the modules: build and test it at the tag (`python Native/ShaderCacheKeeper/build.py`, then `pwsh -File Native/ShaderCacheKeeper/test/run_tests.ps1`), copy `out/xinput9_1_0.dll` to `E:\LOTRAOM_Releases\<channel>\bin\Win64_Shipping_Client\` before the launcher manifest is generated, and record its SHA-256 in the release note; testing channel first ([shader-cache-keeper.md](../features/shader-cache-keeper.md)). When his package carries `TAOM_Map`, add the `FORTS` ids it holds to `RELEASED` in `tools/add_map_fortifications.py` (the one-line command is in the comment above the set), so `add_map_fortifications.py --check` judges only the batch no release carries yet.
   The gate reads every `bin/<platform>/` copy of `TAOM.dll`, `TAOM.Client.dll` and
   `TAOM.Dependencies.dll`, refuses a `TAOM.Client.dll` in a `bin/Win64_Shipping_Server/` (ADR-013), and
   refuses a tag whose `Directory.Build.props` predates the `.dirty` flag. It requires
   `TAOM.Client.dll` only where the release commit's `SubModule.xml` (`git show <sha>:Main/_Module/SubModule.xml`)
   names it, so a tag cut before ADR-013 still gates, refuses when the installed `SubModule.xml` names a
   different set of assemblies than that commit's or does not parse, and `Win64_Shipping_Client` must hold every named assembly, and any other client bin that holds one of them must hold them all. Every run also prints three reports that never refuse and never change the exit code:
   one line per shipped `<module>/SceneObj/<scene>` saying whether `ShaderCache/D3D11/` holds
   `terrain_shaders_header_data.bin` and `compressed_shader_cache.sack`, with the sack's format, a
   summary line and a `WARNING` for any sack whose format differs from the majority of the shipped
   sacks; one line per module-level `<module>/Shaders/D3D11/compressed_shader_cache.sack`, with its
   size and format; and one `JIT optimization` line per shipped copy of `TAOM.dll`,
   `TAOM.Client.dll` and `TAOM.Dependencies.dll` (OFF for the Debug builds TAOM ships on purpose; a bare ON when the
   assembly's `DebuggableAttribute` leaves optimization on; `ON (no DebuggableAttribute on the
   assembly)` when it carries none; `unknown (<reason>)` when the file holds no readable .NET
   metadata or its `DebuggableAttribute` is malformed). A scene without a sack is not an error, and
   scene sacks keep shipping as they are; shipping sacks is the maintainer's call. The `WARNING`
   compares the shipped scene sacks with each other, so a set of them that lags the engine as a
   whole raises none. Module-level sacks, unlike scene sacks, are **left out of the copy, for every
   module.** The maintainer's policy (2026-10-03) is to ship TAOM's and the Armory's compiled sacks
   and never TAOM_Map's, but only after a test proves the game uses a Kit-built sack. Until then
   the report lists each one and says so in one policy line. They are listed, not compared: #448's
   three lagging sacks were exactly these, so while the policy holds a lagging module sack cannot
   reach a build through the packager. `shader_mapping.bin` and `shader_compile_report.log` beside
   them still ship (whether they should is open). The gate proves the DLLs, and it also refuses
   an untracked TAOM file (below).
   Deploys never delete, so the install also holds files from every earlier deploy. Before
   packaging, prune only what neither the tag nor its build owns:
   - **`<game>/Modules/TAOM/` outside `bin/`:** remove what `Main/_Module/` does not hold at the
     tag (`git ls-tree -r --name-only vX.Y.Z -- Main/_Module`). Compare paths
     case-insensitively, as Windows resolves them: the tag spells `GUI/PreFabs/`, the install
     `GUI/Prefabs/`, and an exact comparison deletes every live prefab. Leave
     `RuntimeDataCache*` alone: the packager already excludes it unless `--keep-rdc` asks for it.
     `AssetPackages/pack0.tpac` is what packaging TAOM in the editor writes. In the dev install
     (it has loose `Assets/`) it is a leftover: delete it. In an editor-published channel folder (no
     loose `Assets/`) it is the packed TAOM UI: never delete it, and the check knows the difference.
     The other tpacs there are git-tracked (field camp and refuge prefabs) and stay.
     `DsAssetPackages/` is git-tracked and ships; run `python tools/build_ds_skeleton_pack.py --check`
     before packaging.
     A leftover test-troop file (a `taom_test_` id under `ModuleData/`) is one of these; the packager
     refuses it, dry runs included, so the dry run fails until it is pruned. So does any other TAOM
     file the release commit does not track: under `--require-build`, `stale_module_files` compares
     the copy list (outside `bin/`, `Shaders/D3D11/` and `RuntimeDataCache*`) with the commit's
     `Main/_Module` tree, case-insensitively, and refuses with exit 2 on a stray file, a git
     failure or an empty tree. The message names the folder it checked (the `--source` module, the
     install or a channel folder): delete the file there, or commit it first if it is Kit output the
     repo has not taken yet. `TAOM.Dependencies` is exempt.
   - **Shader sources:** the same dry run refuses a shader in a planned module's
     `Shaders/D3D11/shader_compile_report.log` that has no complete source in the package (a `taom_`
     shader needs all six `.rs` pass files under `TAOM/Shaders/Sources`, so deploy a build first), and
     a report that is missing (the Armory must ship one: run the Modding Kit's module shader compile),
     unreadable or short of its `Total variants` trailer. `--game-root` names the install holding
     `Shaders/Sources`; a channel folder has no such parent, so always pass it.
   - **`<game>/Modules/TAOM.Dependencies/` outside `bin/`:** prune nothing. MCM's UI assets
     (`AssetPackages/`, `EmAssetPackages/`, `GUI/`, `ModuleData/Languages*/`) exist in the install
     only, and no build step recreates them
     ([module-dependencies.md](../modding/module-dependencies.md), "Five folders").
   - **`bin/<platform>/` of both modules:** keep a file whose name the tag tracks under
     `_Module/bin/` (`git ls-tree -r --name-only vX.Y.Z -- Main/_Module/bin Dependencies/_Module/bin`:
     none for TAOM from the NativeSkinFixes removal on, 2 on earlier tags; 44 for TAOM.Dependencies)
     or the tag's build writes. The build writes
     `TAOM.dll`, `TAOM.pdb`, `DryIoc.dll`, `Newtonsoft.Json.dll` and
     `System.Runtime.CompilerServices.Unsafe.dll` into TAOM (plus `TAOM.Client.dll` and
     `TAOM.Client.pdb` into its `Win64_Shipping_Client` and `Win64_Shipping_wEditor`, never `_Server`: the
     server mirror excludes them; Game Pass is not supported, so nothing is built for
     `Gaming.Desktop.x64_Shipping_Client` and the packager refuses any such folder an older install
     or channel folder still holds, so delete it first), and `TAOM.Dependencies.dll`,
     `TAOM.Dependencies.pdb`, `0Harmony.dll`, `Bannerlord.UIExtenderEx.dll`, `MCMv5.dll` and
     `System.Runtime.CompilerServices.Unsafe.dll` into TAOM.Dependencies. That is each project's
     `bin/Debug/net472/` output, every runtime DLL its packages bring in (the other packages are
     compile-only or carry none). The build then mirrors `Win64_Shipping_Client` into `_Server` for
     both modules, and into `_wEditor` for TAOM only. Remove any other file; `.pdb`, `.exp` and `.lib` may stay, since the packager
     never ships them. A retired binary such as `BehaviorTreeWrapper.dll` would otherwise ship; the
     packager refuses the names in `RETIRED_BINARIES` (`tools/package_release.py`), dry runs included.

**The Armory ships in the same release when the TAOM build needs a file it did not have.** Players get
`LOTRLOME_Armory` only from the editor package Mike builds into `E:\LOTRAOM_Releases\<channel>\Modules\`. Since #627
the TAOM build instantiates `taom_howdah_platform` from `LOTRLOME_Armory/Prefabs`; a TAOM build released without an Armory
package carrying it logs `not found` and spawns no howdah platform. Since the same issue the Harad elephant rider
(`troops_harad.xml`) wears `sk_elephant_armor_howdah_elite`, an item only the Armory defines (`LOTRAOM_horses.xml`):
without it the rider's elephant spawns with no harness, no howdah and no crew. Since #805 (2026-10-15) 20 Harad and
Shaghana troops, the six Aserai career kits and 19 Shaghana and Abanissa enlistment rosters name KEYforce's Harad
weapons, which only the Armory's new
`LOTRLOME_items/harad/weapons.xml` defines (with 15 pieces in `LOTRLOME_crafting_pieces.xml`, the Harad rows in
`crafting_templates.xslt` and `weapon_descriptions.xslt`, and the `harad_weapons/regular` packages with their
`RuntimeDataCache` entries); an Armory package without them spawns those troops unarmed. The next Armory package must
also carry `ladder_harad_bow_t7` (`harad/ranged_ladder.xml`), the Serpent and Red Serpent marksmen's bow. Since
2026-10-16 (`d94ee1631`, lotraom-assets `03320a65`) the 22 Harad and Shaghana regular troops and the six Custom Battle
lords also name his maces and javelins (`harad/weapons.xml`, 12 more pieces) and his bardings (`harad/body_armors.xml`,
the `harad_barding` packages), with four more `RuntimeDataCache` entries, and 38 of the 42 Aserai, Shaghana and
Abanissa enlistment kits name his weapons or shields (29 of them a mace or javelin). Check each channel before publishing:
`python tools/validate_moduledata.py --game-modules E:/LOTRAOM_Releases/<channel>/Modules --code BROKEN_ITEM_REF | grep -E "Item\.(hr_st_|sg_st_|sm_hr_st_|sm_sg_st_|sk_(hr|sg)_st_|sk_hr_pz_camel_bard|ladder_harad_bow_t7)"`
must print nothing.

**TAOM_Map ships in the same release when TAOM data names a settlement its package lacks.** Arthedain's towns (`town_AN1` to `town_AN3`) exist only in the live `TAOM_Map`; the `TAOM_Map` in all three release channels had none of them on 2026-10-14. The War Chronicle's `north` event is the smallest dependent: it targets Bree (`town_AN3`) and is Skipped with a logged warning without it. The kingdom itself also needs them: `clans.xml:2175-2176` gives `clan_arthedain_3` the home `Settlement.town_AN3`, `taom_spkingdoms.xml` gives the kingdom the home `Settlement.town_AN2`, and `cultures.json` starts the culture at `town_AN2`. [arthedain.md](../features/arthedain.md) notes that several of a new kingdom's surfaces crash rather than fail quietly, a culture that owns no settlement (#374) among them. The next release must ship a `TAOM_Map` package that contains `town_AN1` to `town_AN3`. Check each channel before publishing: `for t in town_AN1 town_AN2 town_AN3; do grep -c "id=\"$t\"" E:/LOTRAOM_Releases/<channel>/Modules/TAOM_Map/ModuleData/settlements.xml; done` prints 1 three times.

**The next release must ship TAOM_Map and Armory packages whose `language_data.xml` files carry Native's text processor.** A file with a `name` and no `text_processor` resets that language's grammar processor wherever it loads after Native, and a profile with no saved order loads `TAOM_Map` after `TAOM` (its `SubModule.xml` declares `id="TAOM" order="LoadBeforeThis"`), as do launchers that read `DependedModuleMetadatas`. Players get the fix only from a package that carries it (#1026). Check each channel before publishing, with the check that reads no baseline: `BANNERLORD_GAME_MODULES="E:/LOTRAOM_Releases/<channel>/Modules" python tools/check_external_loc_coverage.py --processors-only`, or in PowerShell `$env:BANNERLORD_GAME_MODULES = "E:/LOTRAOM_Releases/<channel>/Modules"; python tools/check_external_loc_coverage.py --processors-only`. Pass means exit 0 and a `PASS:` line. Exit 1 lists the files that still lack the processor; exit 2 means the folder, any `language_data.xml`, or Native was not found. On 2026-10-26 all three channels (testing, patreon, public) reported 15 files.

**TAOM_Map must not name Dale's new clans before the TAOM build that defines them.** `clan_sturgia_10` to `clan_sturgia_14` (the Dale lord expansion, [dale-lord-expansion.md](../features/dale-lord-expansion.md)) exist only in the TAOM build's `characters/clans.xml`, while the live `TAOM_Map/ModuleData/settlements.xml` seats castles to `clan_sturgia_10`, `_11` and `_12` through `Assign-SettlementOwners.py`. A map that names them beside an older TAOM build points castles at undefined clans. Check each channel before publishing: `grep -c 'owner="Faction.clan_sturgia_1[0-2]"'` on `E:/LOTRAOM_Releases/<channel>/Modules/TAOM_Map/ModuleData/settlements.xml` must be nonzero only when `grep -c 'id="clan_sturgia_1[0-4]"'` on that channel's `Modules/TAOM/ModuleData/characters/clans.xml` prints 5.

**TAOM_Map must carry the #1042 name and description repair.** The 4,677 language rows reset from pre-rename names (4,633 for renamed villages and castles, DE "Dorf E2_1" for Linenhold, and 44 for four Dunland towns and castles, DE "Epicrotea" for Galtrev) and the 109 new village descriptions exist only in the live `TAOM_Map` until a package carries them; on 2026-10-27 all three channels still showed "Usanc" for Mearhtun and none of the descriptions. Check each channel before publishing: `BANNERLORD_GAME_DIR=E:/LOTRAOM_Releases/<channel> python tools/oneoff/write_village_descriptions.py --check` exits 0, and `grep -cE 'text="(Usanc|Epicrotea)"' E:/LOTRAOM_Releases/<channel>/Modules/TAOM_Map/ModuleData/Languages/DE/loc_settlements.xml` prints 0 (the `--check` does not read the name rows).

**TAOM and TAOM_Map ship Ered Luin together, or neither does.** Since 2026-10-27 the live `TAOM_Map/ModuleData/settlements.xml` holds Ered Luin's 31 settlements ([ered-luin.md](../features/ered-luin.md)): all carry `Culture.eredluin`, and `town_EL1` to `town_EL3` and `castle_EL1` to `castle_EL5` are owned by `clan_eredluin_1` to `_4`. Only a TAOM build with Ered Luin defines the culture (`taom_spcultures.xml`) and the clans (`characters/clans.xml`). A map with the rows beside an older TAOM points 31 settlements at an undefined culture and 8 at undefined clans; a TAOM with the culture beside a map without the rows leaves `eredluin` owning no settlement, the daily-tick crash `LANDLESS_CULTURE` reports (#374). Check each channel before publishing: `grep -cE 'id="(town_EL[1-3]|castle_EL[1-5])"'` on `E:/LOTRAOM_Releases/<channel>/Modules/TAOM_Map/ModuleData/settlements.xml` must print 8 when `grep -c 'id="eredluin"'` on that channel's `Modules/TAOM/ModuleData/taom_spcultures.xml` prints 1 and `grep -cE 'id="clan_eredluin_[1-4]"'` on its `characters/clans.xml` prints 4, and 0 when both print 0. On 2026-10-27 all three channels printed 0 three times.

**TAOM and TAOM_Map ship Anduin Vale and Angáladh together, or neither does.** The map rows are held back from the live `TAOM_Map/ModuleData/settlements.xml` until the merge sitting ([anduin-vale-angaladh.md](../features/anduin-vale-angaladh.md#merging-this-branch)); once applied they hold 41 settlements, among them `town_AV1`, `town_AV2`, `town_AND1`, `castle_AV1` to `castle_AV4` and `castle_AND1` to `castle_AND4`. All carry `Culture.anduinvale` or `Culture.angaladh`, and the fortifications are owned by `clan_anduinvale_1` to `_4` and `clan_angaladh_1` to `_3`. Only a TAOM build with those cultures defines them (`taom_spcultures.xml`) and the clans (`characters/clans.xml`). A map with the rows beside an older TAOM points settlements at undefined cultures and clans; a TAOM with the cultures beside a map without the rows leaves `anduinvale` and `angaladh` owning no settlement, the daily-tick crash `LANDLESS_CULTURE` reports (#374). Check each channel before publishing: `grep -cE 'id="(town_AV[12]|town_AND1|castle_AV[1-4]|castle_AND[1-4])"'` on `E:/LOTRAOM_Releases/<channel>/Modules/TAOM_Map/ModuleData/settlements.xml` must print 11 when `grep -cE 'id="(anduinvale|angaladh)"'` on that channel's `Modules/TAOM/ModuleData/taom_spcultures.xml` prints 2 and `grep -cE 'id="clan_(anduinvale_[1-4]|angaladh_[1-3])"'` on its `characters/clans.xml` prints 7, and 0 when all three print 0.

**TAOM and TAOM_Map ship the Feral Orcs together, or neither does** (#1063). The `feral_orcs` bandit clan is in the TAOM build's `characters/clans.xml`; its 23 dens `hideout_feral_1` to `hideout_feral_23` exist only in the live `TAOM_Map/ModuleData/settlements.xml` (2026-10-29), and no release channel's map package had them that day. A clan whose culture owns no hideout threw `KeyNotFoundException` on every new game until `Patch125_HideoutClanGuard`; with the guard the clan raids nothing and logs one warning, so a mismatched pair no longer crashes but ships a faction with no dens. Check each channel before publishing: `grep -c 'id="feral_orcs"' E:/LOTRAOM_Releases/<channel>/Modules/TAOM/ModuleData/characters/clans.xml` and `grep -c '<Settlement id="hideout_feral_' E:/LOTRAOM_Releases/<channel>/Modules/TAOM_Map/ModuleData/settlements.xml` must print 1 and 23, or 0 and 0.

**Step 8 is the one that gets skipped**, which is why
[`check-version-tagged.sh`](../../.claude/hooks/check-version-tagged.sh) reminds at turn end
whenever the version in `SubModule.xml` has no tag pointing at any commit. That single condition
catches both a bump committed without a tag and a version that never entered git.

`git push` does not push tags. The tag needs its own refspec, or `--follow-tags`.

## Resolving a crash report to a commit

Given `TaomVersion: v2.0.15.0` in a bundle (the engine renders `v2.0.15` with a fourth component):

```bash
git show v2.0.15                              # the release commit
git show v2.0.15:Main/_Module/SubModule.xml   # exactly what that build declared
git log v2.0.15..v2.0.18 --oneline            # everything that changed after it
git describe --tags --match 'v[0-9]*' <sha>   # which release a given commit is after
```

If the version is one of the five phantoms below, stop — there is nothing to find.

A bundle's `report.txt` (Identity section, `Build:` line) and `manifest.txt` (`TAOM build:` line)
also carry the build stamp, for example `v2.0.0.0 build.20260923-184249Z+c79a585218ad...`. That SHA
is the commit the DLL was compiled from: `git show <sha>`. A `.dirty` suffix means the build also
held uncommitted edits, so the commit is only the nearest known state; `nogit` means git could not
tell. Bundles written before this field existed lack the line: read the `[BuildStamp]` line near the
top of the bundled `taom_debug.log` instead.

**A number a channel carried before git did.** A build can reach a release channel under a number
no commit or tag holds yet, so the tag of that name is not what the player ran. Resolve such a
report by its build stamp, never by the tag:

- **v2.0.33 (testing channel).** The channel's `TAOM.dll` stamp reads
  `build.20261002-194207Z+bc39f6e4...dirty`: `bc39f6e4` plus uncommitted edits, built on 2 October.
  The `v2.0.33` tag (`7b551f87`, cut on 4 October) holds different code and was never packaged. That
  code became `v2.0.34` (`18a4e402`), a release commit that changes only the version fields and
  renames the release note. So `CHANGELOG.md` has no v2.0.34 section (the v2.0.33 section covers the
  code), and the `v2.0.33` tag message still names `docs/releases/v2.0.33-discord.md`, now
  `v2.0.34-discord.md`. v2.0.31 collided the same way on 2026-09-26
  ([lesson](../reviews/lessons/build-tooling-workflow.md)).
- **v2.0.29.5 (patreon and public channels, Bannerlord 1.4.8).** No commit on any branch holds this
  number. The channels' `TAOM.dll` stamp names `8df90444` (branch `fix/public-armory-ids-145`), whose
  `<Version>` in git is v2.0.28.
- **v2.0.35, first testing package (2026-10-18, manifest `2026-10-19-000838`).** Its `TAOM.dll` stamp
  reads `build.20261018-232706Z+6553b8c7...` (clean) and its `TAOM.Dependencies.dll` stamp
  `build.20261018-142454Z+d13e9165...dirty`; no file under `Dependencies/` differs between those
  commits. The package registers five Custom Battle test riders (`taom_test_wildmesh_*`) that the
  `v2.0.35` tag removed, and its `SubModule.xml` pins `TAOM.Dependencies` v2.0.34 against a
  Dependencies module that reads v2.0.35. Mike kept the number for the tag anyway, overriding item 4b
  ([rca-wildmesh-rider-removal-2026-10-18.md](../reviews/rca-wildmesh-rider-removal-2026-10-18.md)):
  the code it ran is the tag's parent code plus the riders, and the package is replaced from the tag
  build.

`/release` Phase 1 item 4b reads every channel's `<Version>` before a number is chosen, so a new
release cannot reuse one a channel already carries, unless the maintainer overrides it as for v2.0.35.

## Historical record: the backfill (2026-08-08)

Eleven `v2.0.x` tags were created retroactively at the commit that **introduced** each version.
Each was gated on reading `<Version>` back out of `Main/_Module/SubModule.xml` at that commit, and
backdated (`GIT_COMMITTER_DATE`) to the commit's author date. A tag marks where a version *began*;
the version then held until the next bump, so it is the release-cut anchor, not proof that only that
commit shipped under the name.

| Tag | Commit | Date | Window ends |
|-----|--------|------|-------------|
| `v2.0.0` | `a1d45ae5` | 2026-05-23 | v2.0.2 |
| `v2.0.2` | `773dc8c2` | 2026-05-26 | v2.0.4 |
| `v2.0.4` | `ae5205c0` | 2026-05-31 | v2.0.5 (same day) |
| `v2.0.5` | `121e972b` | 2026-05-31 | v2.0.7 |
| `v2.0.7` | `8c5c909f` | 2026-06-19 | v2.0.8 |
| `v2.0.8` | `0445a3ae` | 2026-06-30 | v2.0.9 |
| `v2.0.9` | `9286814c` | 2026-07-03 | v2.0.10 |
| `v2.0.10` | `7b7a8dce` | 2026-07-07 | v2.0.13 |
| `v2.0.13` | `777411cc` | 2026-07-13 | v2.0.15 |
| `v2.0.15` | `54667df3` | 2026-07-30 | v2.0.18 |
| `v2.0.18` | `e396263d` | 2026-08-04 | v2.0.20 |
| `v2.0.20` | `094ff0a8` | 2026-08-09 | (current) |

`v2.0.20` was added on 2026-08-12, three days after its bump, on the same terms as the eleven above
(gated on reading `<Version>` back out at that commit, backdated to the commit's author date). It is
a second-generation instance of the same failure, not a leftover from the original sweep: the bump
was a bare `fix(module)` commit rather than a `/release` run, so Phase 1's "current version is
already tagged" pre-flight never executed. `check-version-tagged.sh` did catch it 60 seconds later,
then muted itself per-version and stayed silent for 33 commits. `session-start.sh` now re-asserts the
check every startup so a single missed warning cannot go quiet again.

`v2.0.19` was skipped outright and exists in no commit and no tag.

The pre-2.0 line (`v0.1.0`, `v1.0.0`–`v1.0.3`) was deliberately not tagged — no crash report will
ever be triaged against it.

## The five phantom versions — unresolvable, do not guess

`v2.0.11`, `v2.0.12`, `v2.0.14`, `v2.0.16`, `v2.0.17` **appear in no commit on any branch.**
Verified by reading `Main/_Module/SubModule.xml` at every commit that ever touched it and collecting
the distinct `<Version>` values; the complete set is `v0.1.0`, `v1.0.0`–`v1.0.3`, `v2.0.0`, `.2`,
`.4`, `.5`, `.7`, `.8`, `.9`, `.10`, `.13`, `.15`, `.18`, `.20`.

(`.20` postdates the 2026-08-08 sweep that produced this list and was appended on 2026-08-12. The
phantom set itself is unchanged at five; `.19` never existed, so it is a skipped number rather than a
sixth phantom.)

`v2.0.12` is the one that matters — two player crash reports cite it. A build went out carrying a
version string that was set outside git. **A triage session that meets one of these five should
record "version not in history" and fall back to other evidence** (the `TaomDllSha1` in the bundle,
the engine version, the reported date) rather than inventing a commit range.

`v2.0.6` is not a phantom: it is the `TAOM.Dependencies` module's version, not Main's.

## Related

- [`doc-lookup.md`](doc-lookup.md) — task index
- [`completion-workflow.md`](../ai-includes/completion-workflow.md) — the per-feature ship sequence `/release` sits downstream of
- [`dr3-maintenance.md`](../migration/dr3-maintenance.md) — updating the bundled BUTR stack, which is what forces a Dependencies bump

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/git-and-commits.md](../ai-includes/git-and-commits.md)
- [docs/features/anduin-vale-angaladh.md](../features/anduin-vale-angaladh.md)
- [docs/features/coop-dedicated-server.md](../features/coop-dedicated-server.md)
- [docs/features/dale-lord-expansion.md](../features/dale-lord-expansion.md)
- [docs/features/ered-luin.md](../features/ered-luin.md)
- [docs/features/war-chronicle.md](../features/war-chronicle.md)
- [docs/modding/editing-safely.md](../modding/editing-safely.md)
- [docs/modding/module-dependencies.md](../modding/module-dependencies.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/modding/recipe-new-mod-from-zero.md](../modding/recipe-new-mod-from-zero.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)
- [docs/reference/module-backup-sweep.md](./module-backup-sweep.md)
- [docs/reference/taom-map-settlement-naming.md](./taom-map-settlement-naming.md)
- [docs/reviews/lessons/build-tooling-workflow.md](../reviews/lessons/build-tooling-workflow.md)
- [docs/reviews/rca-hill-troll-2026-09-26.md](../reviews/rca-hill-troll-2026-09-26.md)

<!-- backlinks-end -->
