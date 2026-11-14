TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are reviewing an uncommitted change for issue #1226 in the working tree at `E:\repos\TAOM` (branch `bannerlord-1.5.x`). The change is mostly XML data in a module that git does not track, so read it carefully. You edit nothing: no file, no commit, no build that deploys, nothing in the game install.

## Engine version (read this first)

The installed game is Bannerlord v1.5.7 (`.claude/pinned-game-version.txt`). Read engine code from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file for v1.5.7. The dump at `E:\Decompiled_Bannerlord\` can lag a bump, so use it only to browse. For native code use `python tools/native_decompile.py` (`docs/features/ghidra-native-decompile.md`). Mark a claim UNVERIFIED when you cannot read the evidence.

## The request and the maintainer's decisions

Background: PR #1209 (merged, in tag v2.0.39, commit `6ee3008d7`) gave the dwarves the creature mount rider rows that the Armory had given only to `as_human_warrior`: the warg, spider, elephant and chariot rows, 223 rows in all. The owner had reported a crash when a dwarf mounted a free warg. The cause is unproven (#1226): the missing rows are the leading hypothesis. PR #1209 put the rows in TAOM's own `Main/_Module/ModuleData/action_sets.xml` as partial `as_dwarf_warrior` and `as_dwarf_female_warrior` sets that the engine merges into the Armory's sets.

On 2026-11-14 the maintainer decided: the rows belong in the Armory (`LOTRLOME_Armory`), not in TAOM. This change moves them. Do not re-litigate that decision.

## Implementer's design choices (open to challenge)

- The 223 rows are copied into the existing `as_dwarf_warrior` element of the Armory's `ModuleData/action_sets.xml`, after the two #1087 rows (`act_dwarf_head_butt`, `act_dwarf_drop_kick`) and before the closing tag. The live file and the tracked snapshot `docs/reference/lotrlome-armory-snapshot/action_sets.xml` got the same insertion. They were byte-identical before.
- `as_dwarf_female_warrior` (`base_set="as_dwarf_warrior"`) gets no copy. It inherits the rows. PR #1209 had kept a guard copy there, and its comment said the female block could be deleted once in-game step 2 of #1226 passes. That step has not run, so the guard is gone before the in-game proof. Challenge this if the inheritance evidence is weak.
- TAOM's `action_sets.xml` no longer declares any `as_dwarf_*` set. A test pins that.
- The 8 `act_horse_jump_*` rows and `act_howdah_stand_bow` of the Armory's `action_sets.xslt` template for `as_human_warrior` stay out, as in PR #1209: the horse jumps would replace the dwarf's own jump clips.
- `DwarfMountRiderRowsTests` was rewritten to read the Armory snapshot (always) and the live Armory (category `LiveInstall`). Its live test also serves as the release-channel check, by pointing `BANNERLORD_GAME_DIR` at a channel folder.

## Files in scope

Show a change with `git diff HEAD -- <path>`.

Modified in git:
- `docs/reference/lotrlome-armory-snapshot/action_sets.xml` (a 227 line insertion: 4 comment lines and 223 rows)
- `Main/_Module/ModuleData/action_sets.xml` (the dwarf block and its comment removed; the `as_tcb_horn_*` sets stay)
- `TAOM.Tests/Features/Warg/DwarfMountRiderRowsTests.cs` (rewritten), `TAOM.Tests/Features/Warg/MountRiderFallRowsTests.cs` (one comment)
- `docs/features/war-ram.md`, `docs/ai-includes/creature-mount-authoring.md`, `.claude/rules/traps/creatures-assets.md`, `docs/reference/release-process.md`, `docs/reference/lotrlome-armory-snapshot/README.md`, `docs/reference/bannerlord-animation-system-map.md`

Not in git (read it directly): the live Armory file `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml`. The copy from before the edit is at `E:\Temp\claude\1d198fa1037d6973\9f7d6440-2cf9-4d5d-82fd-bf0aaf106a9f\scratchpad\armory_action_sets.backup.xml`; diff against it.

Context outside the diff: the Armory's `ModuleData/action_sets.xslt` and `action_types.xml`; Native's `ModuleData/action_sets.xml`; `tools/bind_dwarf_attack_actions.py`, `tools/patch_dwarf_action_parity.py`, `tools/audit_action_set_parity.py`, `tools/generate_race_civilian_action_sets.py`, `tools/Generate-ActionSets.ps1`, `tools/data_gates.py`; `docs/reference/release-process.md` ("The Armory ships in the same release"); `docs/reference/armory-guide.md`.

## Facts the author read this turn (check them, do not trust them)

- The snapshot's `as_dwarf_warrior` has no `base_set` and now holds 4,934 actions. A walk of the snapshot's `base_set` chains lists many sets that inherit from it: villager, child, tavern, lord, guard, map, facegen and poses sets, among others.
- In the snapshot every `*_female_warrior` set other than the dwarf's has 0 own actions and relies on `base_set` (for example `as_uruk_female_warrior` > `as_uruk_warrior` > `as_human_warrior`). `as_dwarf_female_warrior` has 550 own actions.
- `python tools/bind_dwarf_attack_actions.py --check --armory <live ModuleData>` exits 0 and reports all four files ok. `python tools/audit_action_set_parity.py` exits 0.
- `DwarfMountRiderRowsTests` and `MountRiderFallRowsTests` pass on this machine (11 tests). Pointed at `E:\LOTRAOM_Releases\public`, the live-Armory dwarf test fails, because that channel's Armory copy (dated 2026-11-06) predates the rows. The `testing` channel is dated the same day, `patreon` 2026-09-26.
- `Main/_Module/ModuleData/action_sets.xml` is now ASCII with CRLF. The Armory files are UTF-8 with BOM and CRLF.

## Known suspects: CONFIRM or DISPUTE each, with file:line evidence

1. **Engine merge and parse.** Read `Module.CreateProcessedActionSetsXMLForNative` and the native parse of `base_set`. With the rows now inside the Armory's own element: is there any remaining module-order dependence? Does any other module (Native, TAOM, TAOM_Map, the Armory's `action_sets.xslt`) declare, patch or template-match `as_dwarf_warrior`, `as_dwarf_female_warrior` or a set derived from them?
2. **Female inheritance.** Does native copy a base set's actions into a child when it parses the child, in document order? Cite the code. The Armory's other female sets rely on it, which is evidence for. But `docs/reviews/rca-elf-cc-facegen-2026-05-22.md` found engine lookup paths that do not fall through `base_set`. Is any rider path (mount, dismount, fall, mount usage actions, `act_mount_*`) one of them? If the female guard copy is needed, say so.
3. **Derived sets.** The set grew by 223 rows and many sets copy it. Does any derived set bind a type among the 223 (a double binding or an override)? Is there an engine limit or a load cost in total action count? (`as_hill_troll_warrior` is a single set of 4,701 actions.)
4. **Byte fidelity and diff purity.** Is `git diff HEAD -- docs/reference/lotrlome-armory-snapshot/action_sets.xml` a pure insertion? Is the live file equal to the snapshot? Are BOM, CRLF and tab indentation intact, with no trailing whitespace? Is the block inside the right element and not inside a comment or a second declaration?
5. **Tools and gates.** Can `bind_dwarf_attack_actions.py`, `patch_dwarf_action_parity.py`, `generate_race_civilian_action_sets.py` or `Generate-ActionSets.ps1` rewrite, move or drop the new block, or flag it? Would a regeneration of the dwarf set lose the rows without any gate failing on a machine with no game install?
6. **Tests.** Which tests can pass vacuously (an empty list, `Single` versus several declarations, `StartsWith("as_dwarf_")`)? Is `Snapshot_FemaleDwarfSet_InheritsTheMaleSetAndCopiesNoRiderRows` an over-pin by the two-edit test (a harmless guard copy fails it)? Does `LiveArmory_ActionSets_EqualTheSnapshot`, which compares two 4.5 MB files whole, over-pin, given that concurrent sessions edit the live Armory and the README says the snapshot has lagged before? Does `MountRiderFallRowsTests` still prove what it claims?
7. **Release coupling.** Is the new paragraph in `docs/reference/release-process.md` right and complete? Is the check command sound, given that `Directory.Build.props` reads `BANNERLORD_GAME_DIR` for the build and the command uses `--no-build`? Does anything else ship, package or restore the Armory (`tools/package_release.py`, an installer, `armory-guide.md`, the snapshot restore steps) and need a mention or a gate? Is a player on a channel whose Armory predates the rows, with a newer TAOM, left exposed?
8. **Stale references.** Is anything still saying that TAOM holds the dwarf rows (docs, code comments, tools, tests, `.claude` rules)? Ignore `.tmp/`, `docs/reviews/raw/` and `docs/reviews/lessons/`.

## What NOT to flag

- Files outside the lists above: other sessions own them.
- That the in-game check is owed, and that the crash cause is unproven (#1226).
- The 12 language files and the translations.
- Style preferences with no behaviour or maintenance cost.

## Output

Report P1, P2 and P3 findings. Cite `file:line` for every claim, and the v1.5.7 decompiled file and line for every engine claim. Mark a claim UNVERIFIED when you cannot read the evidence. State explicitly where you disagree with a design choice above.
