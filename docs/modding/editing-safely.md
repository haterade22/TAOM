# Editing safely

## What this file is

This chapter is the hygiene every other chapter's Modify recipe assumes: how to back up, edit and prove a ModuleData XML change without producing the two incidents TAOM keeps re-living, a backup that the engine parses as a second copy of every id in the file, and a new file that nothing loads until the game restarts. It covers which copy of a file is live, which bytes to preserve, what a comment may contain, what an id may never do, and the one micro-recipe that proves a value reached the game. It is a concept chapter: no attribute tables, worked examples taken verbatim from the shipped files, and every number in it was measured on 2026-09-05 with the command shown beside it.

## Which copy of a file is live

TAOM's data lives in three roots, and only one of them is in git.

| Root | What it holds | How the engine finds it |
|---|---|---|
| `Main/_Module/ModuleData/` (repo) | troops, characters, cultures, party templates, equipment sets, strings | 100 `<XmlName>` rows in `Main/_Module/SubModule.xml`, every one naming a single file <!-- measured: grep -c '<XmlName' Main/_Module/SubModule.xml 2026-09-05 --> |
| `LOTRLOME_Armory/ModuleData/` (game install) | items, crafting pieces, crafting templates, weapon descriptions, monsters, module sounds; `skins.xml` and `action_sets.xml` come in through `project.mbproj` `<file>` rows (lines 3 and 4) instead, see [submodule-and-registration](submodule-and-registration.md) | 33 `<XmlName>` rows: 21 `Items` rows, of which 18 name a FOLDER (`LOTRLOME_items/<culture>`) and 3 name a single file (`LOTRAOM_horses`, `LOTRAOM_weapons`, `LOTRAOM_shields`), plus 8 `Monsters` rows and one row each for `CraftingPieces`, `CraftingTemplates`, `WeaponDescriptions` and `ModuleSounds` <!-- measured: rg -o '<XmlName id="[A-Za-z]*"' "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/SubModule.xml" \| sort \| uniq -c; a loop testing each Items path with -d and -f against "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData/" 2026-09-05 --> |
| `TAOM_Map/ModuleData/` (game install) | `settlements.xml`, `settlements.xslt`, 12 `Languages/<LANG>/loc_settlements.xml`; beside them sit `project.mbproj`, `DistanceCaches/`, 16 Kit stub XML files of 197 to 326 bytes and two copies of SandBox music files that no row in either manifest names, all itemised in [file-catalogue](file-catalogue.md) <!-- measured: ls "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData/Languages/"*/loc_settlements.xml \| wc -l; ls "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData/" (23 entries); wc -c on the 16 stubs; rg -c 'MusicTracks\|MusicInstruments\|settlement_track' on TAOM_Map/SubModule.xml and its project.mbproj (no hits) 2026-09-05 --> | `<XmlName id="Settlements" path="settlements"/>` at `TAOM_Map/SubModule.xml:73` |

`LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/body_armors.xml` is a typical live path. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix.

Three consequences of that split:

- **The repo copy of `settlements.xml` is a stale shadow.** `Main/_Module/ModuleData/settlements.xml` exists (1,023,041 bytes) but no `<XmlName>` row in `Main/_Module/SubModule.xml` names it, so an edit there never reaches the game. Edit `TAOM_Map/ModuleData/settlements.xml`. Source: [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "TAOM_Map settlements.xml is external and live". <!-- measured: ls -la Main/_Module/ModuleData/settlements.xml; grep -n Settlements Main/_Module/SubModule.xml 2026-09-05 -->
- **The build deploys the repo tree verbatim.** `TAOM.csproj`'s `CopyModule` target "recurses `_Module` verbatim and deploys whatever it finds", so whatever sits under `Main/_Module/ModuleData/` lands in the install's `TAOM/ModuleData/` on the next build, backup sidecars included. Source: [module-backup-sweep](../reference/module-backup-sweep.md) lines 43-49.
- **Only the repo root has an automatic gate.** The commit hook runs the validator when a staged path matches `Main/_Module/ModuleData/*.xml` (`.claude/hooks/check-moduledata-validation.sh:71`). The two live modules are not in git, so an edit there stages nothing and gates nothing; run `python tools/validate_moduledata.py` yourself. The 2026-09-01 Armory cleanup found 212 `BROKEN_ITEM_REF` from three deleted shields for exactly this reason: "The Armory is untracked, so the commit hook that would have caught it never fires" ([rca-armoury-keyforce-cleanup-2026-09-01](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md) row 1).

## The `*.xml` glob: why a backup's name is load-bearing

When a registration names a folder rather than a file, the engine does not read a list of files. `MBObjectManager.cs:900-909` strips `.xml` from the registered path, tests `Directory.Exists`, and then takes every file that `new DirectoryInfo(dir).GetFiles("*.xml")` returns, one merge entry each, non-recursively. Vanilla itself uses this form: `SandBoxCore/SubModule.xml:15` is `<XmlName id="Items" path="items" />` and `SandBoxCore/ModuleData/items/` holds 10 files. <!-- measured: ls "$BANNERLORD_GAME_DIR/Modules/SandBoxCore/ModuleData/items" | wc -l 2026-09-05 --> The Armory registers 18 such folders (its other three `Items` rows name a single file), so anything ending in `.xml` inside `LOTRLOME_items/<culture>/` is data.

The rule that follows is the one from [module-backup-sweep](../reference/module-backup-sweep.md) lines 62-71: the LAST extension decides.

| Name | Engine sees it? |
|---|---|
| `action_sets.xml.bak-wargabsorb-20260828` | No. Last extension is not `.xml` |
| `action_sets.bak.xml` | Yes, parsed as real data, duplicating every id in the file |

A duplicated id is not an error the engine reports. It silently shadows one of the two definitions ([armory-guide](../reference/armory-guide.md) line 21), and which one wins is not something you can see from the file. `python tools/validate_moduledata.py` reports the case as `DUPLICATE_ITEM_DEF` (warning) for Armory items and `DUPLICATE_{NPC,CULTURE,ROSTER}_ID` (error) for repo files ([moduledata-validation rule](../../.claude/rules/moduledata-validation.md) code table).

Sidecar names TAOM's tools write, and the sweep recognises, all put the suffix after the real extension: `.bak`, `.bak<N>`, `.bak-<topic>`, `.bak_<topic>`, `.backup`, `.orig`, `.prev`, `.old`, `.tmp`, `.transplanted-<date>` (module-backup-sweep.md lines 32-36; the regex is `tools/sweep_module_backups.ps1:93-99`). Two more facts about them:

- **They must not ship.** `.bak` breaks the Cloudflare distribution, so [release-process](../reference/release-process.md) step 3 (line 71) requires `pwsh tools/sweep_module_backups.ps1` to report 0 files before a release. The sweep moves rather than deletes: the two live modules have no git history, so a sidecar is the only rollback their XML has (module-backup-sweep.md lines 15-29).
- **Git mostly cannot see them.** `.gitignore:24` is `*.bak*`, which hides 8 of the 9 sidecars the repo tree held on 2026-09-05; the ninth, `sp_battle_scenes.xml.bak_scenes`, was committed before the rule (`0ed2cf38`) and is tracked, so a sweep that moves it shows up in `git status` as a deletion. <!-- measured: find Main/_Module -type f -name "*.bak*" | wc -l; git ls-files Main/_Module | grep -c bak; git check-ignore -v Main/_Module/ModuleData/sp_battle_scenes.xml.bak_scenes (exit 1); git log --oneline -1 -- Main/_Module/ModuleData/sp_battle_scenes.xml.bak_scenes 2026-09-05 -->

The flip side bites when you search. A recursive `grep` over a live folder reads the sidecars too and hands back values from an old snapshot as if they were current; the worked example in the lessons file is a `Culture.rohan` hit that came only from two `.bak-*` files (`rohan` is not a culture id in TAOM; Rohan's troops carry `Culture.vlandia`, which is why the only hits were stale sidecars). <!-- measured: rg -n 'id="rohan"' Main/_Module/ModuleData/taom_spcultures.xml (no hits); rg -c 'Culture.vlandia' Main/_Module/ModuleData/troops/troops_rohan.xml 2026-09-05 --> Search the exact file the registration names, or add `--include='*.xml'` ([xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "grep -r over a live ModuleData folder reads .bak files the engine never loads").

## Keep the bytes: BOM and line endings

The repo's ModuleData is not one encoding shape, so an editor that "normalises" on save rewrites every line and buries your two-attribute change in a whole-file diff. Measured over the 356 `.xml`, `.xslt` and `.json` files under `Main/_Module/ModuleData/` on 2026-09-05:

| Shape | Files |
|---|---|
| UTF-8 with a BOM (`EF BB BF`) | 14 <!-- measured: python -c "import pathlib;f=[p for p in pathlib.Path('Main/_Module/ModuleData').rglob('*') if p.suffix in('.xml','.xslt','.json')];print(len(f),sum(p.read_bytes().startswith(b'\xef\xbb\xbf') for p in f))" 2026-09-05 --> |
| Majority terminator CRLF (`\r\n`) | 178 |
| Majority terminator doubled CR (`\r\r\n`) | 133 |
| Majority terminator LF (`\n`) | 43 |
| Single-line files with no terminator | 2 |
| Files carrying two shapes at once | 49 <!-- measured: python -c "import pathlib,collections;c=collections.Counter();F=[p for p in pathlib.Path('Main/_Module/ModuleData').rglob('*') if p.suffix in('.xml','.xslt','.json')];B=[p.read_bytes() for p in F];K=[{'CRLF':b.count(b'\r\n')-b.count(b'\r\r\n'),'CRCRLF':b.count(b'\r\r\n'),'LF':b.count(b'\n')-b.count(b'\r\n')} for b in B];[c.update([max(k,key=k.get)]) if sum(k.values()) else c.update(['none']) for k in K];print(dict(c),'two_shapes',sum(sum(v>0 for v in k.values())>1 for k in K))" 2026-09-05 --> |

The 14 BOM files are `characters/lords.xml`, `heroes.xslt`, `lords.xslt`, `spclans.xslt`, `spkingdoms.xslt`, `taom_partyTemplates.xml`, `taom_xslt_strings.xml`, `module_sounds.xml`, `lotr_dwarf_voice_def.xml`, `VoiceDefinitions/LOTR/lotr_warg_voice_def.xml`, and `troops/troops_{erebor,goblin,gundabad,isengard}.xml`. In the game install, `TAOM_Map/ModuleData/settlements.xml` has one too. <!-- measured: head -c 3 "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData/settlements.xml" | xxd 2026-09-05 --> The doubled CR concentrates in `Languages/**/std_taom_*.xml` and comes back on every rebuild of those files, so it is not a defect to "fix" ([TRANSLATOR_GUIDE](../localization/TRANSLATOR_GUIDE.md) "Line endings and encoding").

What that means for you:

- **Hand edits:** open the file, change the value, save, and let nothing else change. Check your editor's encoding and line-ending indicators before you save, and turn off any "convert line endings" or "add BOM" setting. Then `git diff --stat` (repo files) must show a handful of lines, not the whole file.
- **Scripted edits:** read bytes, edit, write bytes, and re-emit the file's own terminator. The three sanctioned idioms and the forbidden mixed shape are in [tools/README.md](../../tools/README.md) "XML I/O convention", and the `\r?\n` trap that matches nothing on a doubled-CR file is explained in [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "Line endings vary file by file under ModuleData".
- **The rule in force** is the Formatting section of [xml-data](../../.claude/rules/xml-data.md) lines 108-121: round-trip the bytes instead of normalising.

## Comments: two rules

**No `--` inside a comment.** The XML specification forbids it, and the engine's loader is more permissive and less verbose than a real parser, so the file that breaks surfaces as a black-screen crash log with no file and line. `taom_partyTemplates.xml` shipped a comment using `--` as a separator on 2026-05-27; it passed five review agents and was caught only by a parser ([xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "Smoke-test every new/modified ModuleData XML with an actual parser before commit"). The smoke test below catches it before anything else runs.

**No comment inside a `<Culture>` list container.** `CultureObject.cs:367-510` walks each child of `<Culture>` by name and, inside `default_policies`, loops `foreach (XmlNode childNode in item5.ChildNodes)` and reads `childNode.Attributes["id"].Value` (lines 371-373). A comment node carries no attribute collection, so that read throws before the culture finishes loading. Every list container in that method has the same shape: `male_names`, `female_names`, `clan_names` (377-397), `cultural_feats` (398-410), `possible_clan_banner_icon_ids` (411-418), and every template and reference list from `notable_templates` to `available_ship_hulls` (419-509), which go through `ReadObjectReferenceFromXml` and index `node.Attributes[attributeName]` first (`MBObjectManager.cs:1497-1503`). A comment placed directly under `<Culture>`, between the containers, is safe: its name matches no branch and falls to the `continue` at lines 499-504. The `#332` sweep note records the trigger as "XML comments inside `notable_templates`", 0 on this branch and 3 on the `1.5.x` line (`CHANGELOG.md:5427-5429`). Put your notes between containers, never inside one.

## Ids: never rename, and case matters

- **An id is a save key.** `MBObjectBase.StringId` is `[SaveableProperty(1)]` (`MBObjectBase.cs:11-12`): the save carries the id string, and a renamed id leaves every saved reference pointing at nothing. Settlement ids are recorded as save-bound (xslt-moduledata lessons, "TAOM_Map settlements.xml is external and live"), and a string `id` is the localisation key that the 12 language folders under `Main/_Module/ModuleData/Languages/` hang their text on ([TRANSLATOR_GUIDE](../localization/TRANSLATOR_GUIDE.md) "File Format", line 60). <!-- measured: ls -d Main/_Module/ModuleData/Languages/*/ \| wc -l 2026-09-05 --> Change the `name="{=key}Display text"` after the closing brace instead; that is the part players see.
- **A rename also strands prose.** Lore text names characters by literal name in `settlements.xml` `text=` blurbs and the string files, so after any rename grep ALL of `Main/_Module/ModuleData/` for the old name, with no file filter (xslt-moduledata lessons, "After renaming any entity, grep ALL of ModuleData for the OLD name").
- **Retire and add, do not rename.** Leave the old entry in place, add the new one, and move references. A delete without moving references is what produced the 212 `BROKEN_ITEM_REF` above; the validator names each one. Retiring a whole file is a rename to a non-`.xml` suffix plus the removal of its row, the steps in [submodule-and-registration](submodule-and-registration.md) "Remove a registration without deleting the file"; the 2026-08-28 Armory fix retired `Animations/action_types_lotr_misc.xml` that way, as `.bak-superseded-20260828` ([lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md) lines 112-118). Whole-entity retirement across files is [recipe-retire-content](recipe-retire-content.md).
- **Ids are case-sensitive.** The engine's registry per type is a `Dictionary<string, T>` created with the default comparer (`MBObjectManager.cs:75,100`), so `Item.sk_gd_anf_inf_chainmail_a` and `Item.Sk_gd_anf_inf_chainmail_a` are two different keys (the real one is a Gondor body armour, `LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/body_armors.xml:490`), and a reference in the wrong case does not find the object. Filesystem-backed names (scene folders) resolve case-insensitively on Windows, so an audit that compares them must lower-case both sides ([vanilla-data-comparison](../../.claude/rules/vanilla-data-comparison.md) line 39), while git pathspecs stay case-sensitive ([localization-ui lessons](../reviews/lessons/localization-ui.md), the `GUI/PreFabs` entry). Whether mesh names inside a `.tpac` package are matched case-sensitively was not determined from the engine; TAOM's catalogue tool matches mesh names to item references case-sensitively (`tools/generate_armory_catalogue.py:307`, against the set built at lines 457-458), and only its token classifier lower-cases a name (line 180).

## Worked example

### A sidecar beside its live file

The gondor armour folder on 2026-09-05, exactly as `ls` prints it:

<!-- excerpt file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor" -->
```
arm_armors.xml
body_armors.xml
body_armors.xml.bak-deadmesh-20260901130757
head_armors.xml
leg_armors.xml
shoulder_armors.xml
starter_armors.xml
```
<!-- measured: ls -1 "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/" 2026-09-05 -->

1. **The name.** `body_armors.xml.bak-deadmesh-20260901130757` is `<live file name>` + `.bak-` + `<topic>` + `-` + `<timestamp>`. The engine's `*.xml` glob skips it because its last extension is `.bak-deadmesh-20260901130757`, not `.xml`; the sweep recognises it because the suffix sits after the real extension.
2. **It is a different file, not a copy.** The live file is 67,221 bytes and the sidecar 72,131; `cmp` reports the first difference at line 11. That is the point: the sidecar is the pre-edit state of the 2026-09-01 dead-mesh pass, and it is the only rollback this untracked file has. <!-- measured: ls -la and cmp on the two files under "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/" 2026-09-05 -->
3. **Six real data files load from this folder,** and the seventh entry does not. If the sidecar had been named `body_armors.bak.xml` there would be seven, and every id in `body_armors.xml` would exist twice.

### The parser smoke test

Before the validator and before the game, parse the file with a real XML parser. PowerShell has one built in:

```
pwsh -Command '[xml]$x = Get-Content -Raw "Main/_Module/ModuleData/taom_partyTemplates.xml"; "OK"'
```

On 2026-09-05 that printed `OK` for the shipped file. <!-- measured: pwsh -NoProfile -Command '[xml]$x = Get-Content -Raw "Main/_Module/ModuleData/taom_partyTemplates.xml"; "OK"' 2026-09-05 --> Against a four-line scratch file (`<root>`, `<!-- gondor -- footman -->`, `<a/>`, `</root>`, no indentation) it printed, in red, a `MetadataError` whose last line ends:

```
Error: "An XML comment cannot contain '--', and '-' cannot be the last character. Line 2, position 13."
```

followed by `OK`, because the plain form keeps going after the failed cast and still exits 0; the position counts from the start of the line, so an indented comment reports a larger number. <!-- measured: pwsh -NoProfile -Command on a scratch bad_comment.xml with and without a two-space indent, exit code 0, positions 13 and 15, 2026-09-05 --> If you want the exit code to mean something (a script, a checklist), use the wrapped form, which printed only the error and exited 1 on the same bad file, and `OK` with exit 0 on the shipped one: <!-- measured: same two files with the try/catch form 2026-09-05 -->

```
pwsh -Command 'try { [xml]$x = Get-Content -Raw "<file>"; "OK" } catch { $_.Exception.Message; exit 1 }'
```

The parser is stricter than the engine, which is what you want: unescaped `&`, `<` or `>` in an attribute, a mismatched tag, a duplicated attribute, a stray BOM in the middle of a file, and `--` in a comment all fail here with a line number, and fail in the engine with nothing.

## When an edit reaches the game

The engine reads the manifests once, at process launch (`Module.cs:267` and `1026-1032`; `XmlResource.cs:142-182`), and never during play; each `LoadXML(id)` call then resolves the paths, globs any folder registrations, merges the files and registers the objects when a campaign starts or a save loads (`MBObjectManager.cs:786-797` and `877-909`). The mechanics are [submodule-and-registration](submodule-and-registration.md); which ids re-read on which path:

| XML ids | Re-read when | Source |
|---|---|---|
| `Items`, `EquipmentRosters`, `partyTemplates` | new campaign AND save load | `Campaign.cs:1471-1473`, called from both `1396-1398` (saved campaign) and `1520-1524` (new campaign) |
| `NPCCharacters`, `WorkshopTypes`, `LocationComplexTemplates`, `Settlements` | new campaign AND save load | `SandBoxManager.cs:362-381` |
| `Heroes`, `Kingdoms`, `Factions` | new campaign only (`if (!isSavedCampaign)`) | `SandBoxManager.cs:363-375` |

So a lord, clan or kingdom edit is "new campaign only" by engine design, while a troop or item edit re-reads on a save load. Per-field survival is another matter: hero skills bake from `skill_template` at creation and keep their saved values (data-content-cultures lessons, "Hero skills come from skill_template"), and any value a feature persists in the save behaves the same way.

**TAOM's rule is still a full game restart for any item or equipment change, and the validator cannot stand in for it.** On 2026-06-30 twelve new `starter_armors.xml` files were dropped into already-registered `LOTRLOME_items/<culture>/` folders; `validate_moduledata.py` passed, the build and tests were green, and every non-Gondor character was naked after picking a career until the game was restarted. The validator parses files off disk in its own process; it proves references resolve on disk now, not that the running engine loaded them ([moduledata-validation rule](../../.claude/rules/moduledata-validation.md) "PASS ≠ in-game loaded" (line 156); [data-content-cultures lessons](../reviews/lessons/data-content-cultures.md) "A NEW item XML file only loads at process launch"; [starting-equipment-tuning](../features/starting-equipment-tuning.md) "Verifying in-game"). Restart, then look.

## Recipes

### The universal micro-recipe: change one value and prove it loaded

Every Modify recipe in the file chapters is this recipe with a different value. The example uses the first troop in `Main/_Module/ModuleData/troops/troops_gondor.xml`:

<!-- example file="Main/_Module/ModuleData/troops/troops_gondor.xml" id="gondor_loss_lumberman" -->
```xml
  <NPCCharacter
      id="gondor_loss_lumberman"
      default_group="Infantry"
      level="6"
      name="{=aom_gondor_loss_lumberman_name}[Gondor] Lossarnach Lumberman"
      occupation="Soldier"
      is_basic_troop="true"
      culture="Culture.gondor">
```

1. **Find the live copy** (first section). This one is a repo file, deployed by the build; an Armory or TAOM_Map file is edited in place in the install.
2. **Make the sidecar first:** copy `troops_gondor.xml` to `troops_gondor.xml.bak-<topic>-<yyyymmdd>` in the same folder. Suffix after `.xml`, never before.
3. **Change one value.** Append ` TEST` to the display text after the closing brace: `[Gondor] Lossarnach Lumberman TEST`. Leave `{=aom_gondor_loss_lumberman_name}` and `id=` untouched (the id is a save key; the brace text is the localisation key). No language file carries that key, so the inline text is what every language shows. <!-- measured: grep -rl aom_gondor_loss_lumberman_name Main/_Module/ModuleData/Languages/ | wc -l 2026-09-05 --> The engine reads `name` at `BasicCharacterObject.cs:318`. A numeric alternative is `level`, read at `BasicCharacterObject.cs:487-488` and shown as "Level" in the village recruit tooltip (`RecruitVolunteerTroopVM.cs:356`); keep it inside the same tier (tier is a function of level, see [troops](troops.md)) so you are testing loading and not the upgrade ladder.
4. **Save without changing bytes:** same encoding, same line endings. For a repo file, `git diff --stat Main/_Module/ModuleData/troops/troops_gondor.xml` must report one changed line.
5. **Parse it:** the smoke test above, on the file you edited.
6. **Validate it:** `python tools/validate_moduledata.py`. On 2026-09-05 the shipped data reported `0 error(s), 94 warning(s)`, all `INCONSISTENT_ARMOUR_SLOT`; anything new in that summary is yours. <!-- measured: python tools/validate_moduledata.py 2026-09-05 -->
7. **Build if it is a repo file** (`./build.ps1`), so the edit is deployed. Skip for a live-module file.
8. **Close Bannerlord completely, start it again,** and load a save or start a campaign (troops re-read on both, `SandBoxManager.cs:362`). Open the Encyclopedia's units page, or a Gondor village's recruit menu, and find `[Gondor] Lossarnach Lumberman TEST`. Seeing it is the proof; a green validator is not.
9. **Revert:** undo the one line (or copy the sidecar back), rebuild if a repo file, and keep the sidecar until the real edit is verified. Sweep sidecars before any release.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Back up a live file before you touch it

1. Copy `<file>.xml` to `<file>.xml.bak-<topic>-<yyyymmdd>` in the same folder. The topic is one word naming the edit (`deadmesh`, `genderflag`, `fellwarg` are shipped examples); the date makes a second same-day backup not overwrite the first.
2. Confirm with `ls` that the new name ends in your suffix and not in `.xml`.
3. Confirm the sweep sees it: `pwsh tools/sweep_module_backups.ps1` (report only, nothing moves) lists it under its module. On 2026-09-05 the report found 40 files, 441.9 MB: 9 in the repo tree, 13 in the Armory, 9 in the install's `TAOM`, and 9 scene backups in `TAOM_Map`. <!-- measured: pwsh -NoProfile -File tools/sweep_module_backups.ps1 2026-09-05 -->
4. Leave it in place until the edit is verified in game, then let the pre-release sweep quarantine it. A tool's `--revert` needs the sidecar beside the live file, so after a sweep, restore from the quarantine before reverting (module-backup-sweep.md lines 120-121).

Check: `pwsh tools/sweep_module_backups.ps1`
Takes effect: live
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A backup ending in `.xml` inside a folder registration is data.** Every id in it exists twice and the engine shadows one without a message. `MBObjectManager.cs:903`; [module-backup-sweep](../reference/module-backup-sweep.md) lines 62-71.
- **A new file in a registered folder is invisible until a full restart, and every validator says PASS.** The 2026-06-30 naked-until-restart incident. [moduledata-validation rule](../../.claude/rules/moduledata-validation.md) "PASS ≠ in-game loaded" (line 156); [data-content-cultures lessons](../reviews/lessons/data-content-cultures.md).
- **`--` inside a comment crashes the loader with no file and line;** a parser rejects it with one. [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "Smoke-test every new/modified ModuleData XML".
- **A comment inside a `<Culture>` list container throws during load.** `CultureObject.cs:371-373` and the sibling loops through line 509; `MBObjectManager.cs:1499-1503`.
- **A `.bak` XML makes `grep -r` lie.** A recursive search over a live folder returns values from the sidecar. [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "grep -r over a live ModuleData folder reads .bak files".
- **A normalising editor turns a one-line edit into a whole-file rewrite** and, on the 133 doubled-CR files, a `\r?\n` regex matches nothing and reports "nothing to do". [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "Line endings vary file by file"; [tools/README.md](../../tools/README.md) "XML I/O convention".
- **The repo `settlements.xml` accepts edits and does nothing.** It is not registered. [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "TAOM_Map settlements.xml is external and live".
- **A live-module edit gets no commit hook.** `.claude/hooks/check-moduledata-validation.sh:71` matches repo paths only; the Armory and TAOM_Map are untracked. [rca-armoury-keyforce-cleanup-2026-09-01](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md) row 1.
- **A renamed id is a dangling save reference and stale prose.** `MBObjectBase.cs:11-12`; [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md) "After renaming any entity".
- **A wrong-case reference finds nothing.** The registry is a default-comparer `Dictionary<string, T>`. `MBObjectManager.cs:75,100`.
- **Sidecars in `Main/_Module/` are invisible to git and redeployed by every build.** `.gitignore:24`; [module-backup-sweep](../reference/module-backup-sweep.md) lines 43-56.
- **A sidecar with no live sibling is the sole copy, not a backup.** The sweep aborts an `-Apply` run past `-MaxOrphans` (default 3) for this reason. [module-backup-sweep](../reference/module-backup-sweep.md) lines 38-41.

## Numbers in this chapter

All measured 2026-09-05 from the repo root (`$BANNERLORD_GAME_DIR` is the game install's root, which `tools/sweep_module_backups.ps1:50-57` also reads).

| Number | Command |
|---|---|
| 100 `<XmlName>` rows in TAOM's `SubModule.xml`, none naming a folder | `grep -c '<XmlName' Main/_Module/SubModule.xml`, then a loop testing each `path=` value with `-d` against `Main/_Module/ModuleData/` (no directory hits) |
| 33 `<XmlName>` rows in the Armory's `SubModule.xml`: 21 `Items` (18 folders, 3 files), 8 `Monsters`, 4 single rows | `rg -o '<XmlName id="[A-Za-z]*"' "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/SubModule.xml" \| sort \| uniq -c`, then a loop testing each `Items` path with `-d` and `-f` against `ModuleData/` |
| 10 files in vanilla's `items` folder | `ls "$BANNERLORD_GAME_DIR/Modules/SandBoxCore/ModuleData/items" \| wc -l` |
| 1,023,041 bytes, repo `settlements.xml`; 0 `Settlements` rows in the repo `SubModule.xml` | `ls -la Main/_Module/ModuleData/settlements.xml; grep -n Settlements Main/_Module/SubModule.xml` |
| 356 ModuleData files, 14 with a BOM | the `python -c` one-liner in the BOM table row |
| 178 CRLF / 133 doubled CR / 43 LF / 2 no terminator, 49 files with two shapes | the `python -c` script in the two-shapes table row |
| 9 sidecars in the repo tree, 8 ignored, 1 tracked (`sp_battle_scenes.xml.bak_scenes`, commit `0ed2cf38`) | `find Main/_Module -type f -name "*.bak*" \| wc -l; git ls-files Main/_Module \| grep -c bak; git check-ignore -v <that path>; git log --oneline -1 -- <that path>` |
| 13 sidecars under the Armory's `ModuleData` | `find "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData" -type f \( -name "*.bak*" -o -name "*.backup" -o -name "*.orig" -o -name "*.prev" -o -name "*.old" \) \| wc -l` |
| 40 files, 441.9 MB in the sweep report (9 repo, 13 Armory, 9 TAOM install, 9 TAOM_Map scene backups) | `pwsh -NoProfile -File tools/sweep_module_backups.ps1` |
| 7 entries in the gondor folder listing, 6 of them `.xml` | `ls -1 "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/"` |
| 67,221 bytes live / 72,131 bytes sidecar, first difference at line 11 | `ls -la` and `cmp` on `gondor/body_armors.xml` and its `.bak-deadmesh-20260901130757` sidecar |
| 0 language files carrying `aom_gondor_loss_lumberman_name` | `grep -rl aom_gondor_loss_lumberman_name Main/_Module/ModuleData/Languages/ \| wc -l` |
| 12 language folders in the repo; 12 `loc_settlements.xml` in `TAOM_Map` | `ls -d Main/_Module/ModuleData/Languages/*/ \| wc -l; ls "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData/Languages/"*/loc_settlements.xml \| wc -l` |
| 23 entries in `TAOM_Map/ModuleData/`, 16 of them Kit stubs of 197 to 326 bytes, 0 manifest rows naming the two music files | `ls "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData/" \| wc -l`; `wc -c` on the 16 stub files; `rg -c 'MusicTracks\|MusicInstruments\|settlement_track'` on `TAOM_Map/SubModule.xml` and `TAOM_Map/ModuleData/project.mbproj` |
| 0 errors, 94 warnings from the validator | `python tools/validate_moduledata.py` |
| `OK` / exit 0 on `taom_partyTemplates.xml`; the `--` error at line 2, position 13 (15 with a two-space indent) / exit 1 on the bad comment in the wrapped form, exit 0 in the plain form | the two `pwsh -Command` lines in the smoke-test section, on the shipped file and on two scratch copies of the four-line bad file |
| 212 `BROKEN_ITEM_REF` from 3 deleted shields | quoted from [rca-armoury-keyforce-cleanup-2026-09-01](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md) row 1 |

## Read next

- [tools/README.md](../../tools/README.md), "XML I/O convention": the byte-faithful idioms and the backup-suffix rule.
- [xml-data rule](../../.claude/rules/xml-data.md), "Formatting": the BOM and line-ending rule in force.
- [moduledata-validation rule](../../.claude/rules/moduledata-validation.md): the validator's codes, its three-module scope, and "PASS ≠ in-game loaded" (line 156).
- [moduledata-validation feature doc](../features/moduledata-validation.md): "Module coverage at a glance".
- [module-backup-sweep](../reference/module-backup-sweep.md): the sweep, the quarantine, and the last-extension invariant.
- [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md): the parser smoke test, the `.bak` grep trap, the line-ending capture rule, the rename grep.
- [data-content-cultures lessons](../reviews/lessons/data-content-cultures.md): "A NEW item XML file only loads at process launch".
- [starting-equipment-tuning](../features/starting-equipment-tuning.md), "Verifying in-game": the incident behind the restart rule.
- [TRANSLATOR_GUIDE](../localization/TRANSLATOR_GUIDE.md): why string ids never change and where the doubled CR comes from.
- [release-process](../reference/release-process.md): where the sweep gates a release.
- [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md): a retirement done with a non-`.xml` suffix.
- [armory-guide](../reference/armory-guide.md): the duplicate-id shadowing rule.
- [vanilla-data-comparison rule](../../.claude/rules/vanilla-data-comparison.md): case-insensitive scene and asset matching.
- [rca-armoury-keyforce-cleanup-2026-09-01](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md): the untracked-Armory hook gap.
