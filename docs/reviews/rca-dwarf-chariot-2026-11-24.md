# RCA: dwarven war chariot (deep review and Codex pre-review, 2026-11-24)

## Top line

The dwarven war chariot is data only: two meshes for the harness slot, a ram team for the mount slot, three items in
the live Armory and two Ironpass troops. `/deep-review` at tier 1 (lenses 2, 5, 7 and Tooling) with a Codex
pre-review found 2 HIGH and 7 MEDIUM findings and no engine incompatibility. Two more faults were found by the full
test suites before the lenses reported. The second wave (lenses 3, 4, 6, a Tooling re-review and a Codex re-review)
found one more HIGH, in the tool that fixed the first two, and three MEDIUM guards. All of them were corrected in
the same session, before any commit. No finding reached a player. Nothing was checked in game.

The HIGH findings are in two scripts that wrote the live Armory from outside the repo. They did no damage: the
Tooling lens compared the live item file with its three backups byte by byte and found only the intended edits.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Tooling) | The install scripts' "the Kit cooked this package" guard took the first `.tpac` whose file name CONTAINED the wanted name and whose bytes held it. The material `dwarf_war_chariot_iron` passed on a texture package, and the mesh `dwarf_war_chariot` also matched `dwarf_war_chariot_b_geo.tpac`. | Guard weaker than its message | I wrote the guard for the first mesh and then reused it for materials without asking what a material package looks like on disk. I saw the material packages had no cache entry, saw the guard print True for them anyway, and did not ask why. | The guard is now `missing_packages` in `tools/apply_dwarf_chariot_armory.py`: an exact file name per kind (`<mesh>_geo.tpac` with a cache entry, `<material>_mtl.tpac` without one), pinned from both sides by `CookedPackageGuardTests`. Lesson in `build-tooling-workflow.md`. |
| F2 | HIGH (Tooling) | Each script's `--rollback` copied back a whole-file backup taken at a different time. Three scripts and one inline edit had each saved the same file, so a rollback of the first would have dropped the team colour flag and the chariot b item while `troops_erebor.xml` still named it. No script could restore the names file. | Rollback by snapshot | Each script was correct alone. Nobody, I included, looked at the four writers of one file together. | `--remove` in the new tool takes out only its own bytes and refuses when the block differs (DRIFT). `DriftAndRemoveTests` pin that a later edit survives and that the first bytes come back. Same lesson entry. |
| F3 | MEDIUM (Data flow, Codex P2) | The two troops had no row in `special_resources/troop_resource_costs.xml`. The Ironpass line pays Gems from level 31, so the chariot branch was a free way past the Ram Marshal's 5 Gems, with no upkeep. | A new troop missing from a per-id ledger | I copied the Ram Marshal's kit and skills and ran the validator, which reads none of the ledgers. | Rows added on the line's scale. Lesson in `data-content-cultures.md` with the list of ledgers and the command that finds them. |
| F4 | MEDIUM (test suite) | No row in `TroopWeights/troop_weights.xml`: Ram Vanguard 2.0 upgraded into War Chariot 1.0. `NoUpgrade_LowersTheWeight` failed. | Same as F3 | Same as F3. The full C# suite ran after the review was launched, not before. | Rows added at 3.0 (the level rule). Same lesson. |
| F5 | MEDIUM (XML, Codex P2) | The line's two-handed axe beside a shield needs a ratchet row per troop in `tools/audit_polearm_shield_parity.py`; the copies had none and the gate went red. | Same as F3 | Same as F3. | Rows added with the line's reason. Same lesson. |
| F6 | MEDIUM (XML) | The three items and three name rows existed only in the live, unversioned Armory and in backups outside git. A reinstall would have left two troops naming missing items. | Unversioned edit with no repo-held spec | The lesson already exists (`data-content-cultures.md`, "An edit to an unversioned module needs a repo-held spec and a re-apply tool with a check", 2026-11-14). I treated the first install as a quick look in game and never came back to it. This is a repeat. | `tools/apply_dwarf_chariot_armory.py` holds the rows, `--check` compares the live files with them, and `LiveArmoryTests` runs that check in the tool suite. The existing lesson gets a second source line. |
| F7 | MEDIUM (XML, Data flow) | Generated ledgers behind: the armour colour families (two new harness items), the armour tier list (new wearers), the cover flag snapshot and the external localization baseline (+3 English-only Armory rows per language). | Same as F3 | Same as F3. | Each regenerated or moved; only the chariot rows of the colour table were taken, because 24 older rows of other items also differ there. |
| F8 | MEDIUM (Engine) | The reason written beside `DWARF_MOUNT_IDS` and in the doc was wrong: "the rider stands, so the horse seat is not used". The engine places every rider the same way, his clip against the mount's sit bone. The dwarf plays clips made for the human skeleton, which is the condition `MOUNTED_DWARF` exists for. | Engine behaviour inferred from a name | I read "stands" off clip names and wrote it as mechanism. | Comment and doc corrected to what was read (`rider_sit_bone="chariot_bone"`, the rider rows) and to what only the game can show. |
| F9 | MEDIUM (Engine, proven with the skeleton dumps) | The ram team item copied `skeleton_scale="empire_horse"` from the Wainrider item. The engine applies a scale by bone index of the scale's own skeleton. On `horse_skeleton_chariot` the five indices are the rig root and bones of one animal, so one ram would be stretched by up to 15 percent. | Copied attribute, never traced | The attribute came with the upstream pack's item and I carried it to the new row as "what the proven item has". The proven item was never checked for it either. | Removed from the ram team row; `test_the_ram_team_row_carries_no_skeleton_scale` pins it. Lesson in `animation-skeleton.md`. The Wainrider item still has it: open, see below. |
| F10 | MEDIUM (tool suite, from the 2026-11-23 rig port) | `test_armory_kick_typed_actions` failed on the live Armory: the port typed `act_chariot_rear` and `act_chariot_kick` as `actt_kick`. Both are bound to rider clips too, and the kick clip is an idle clip, which is the #1499 hang class. | Unversioned edit made without the tool suite | The port's live edits were made on 2026-11-23 and the tool suite was not run after them. | Retyped in the live `action_types.xml`: `actt_rear` (as the vanilla horse) and no type. The test passes. The repo's snapshot of that file belongs to the port's branch. |
| F11 | HIGH (Tooling re-review) | In the new tool the search for the end of the anchor item looked for the next `<Item`, which also matches `<ItemComponent>`. Every real item holds one, so `--apply` reported a missing anchor and could never put the rows back after a reinstall: the one case the tool exists for. It failed closed. | Test fixture simpler than the real document | The fixture's anchor item had no `<ItemComponent>`, and the test against the live install ran `--check` only while the rows were present, so no test reached the insertion on a real item. All 21 tests passed for that reason. | The search is `<Item[\s/>]`; the fixture's anchor now holds an `<ItemComponent>` (19 tests went red on it before the fix), and `test_remove_then_apply_on_a_copy_of_the_live_files_gives_the_live_bytes_back` runs the restore on the real documents. Lesson added to `build-tooling-workflow.md`. |
| F12 | MEDIUM (Codex re-review, three guards) | The names anchor was searched in raw text, so an anchor inside a comment took the rows into the comment; ids were found by the literal `id="..."`, so rows written with single quotes read as absent and a second block went in; `--check` parsed only the files it would change, so a document that no longer parsed passed while the rows were still in it. | Text matching where the document should be read | The first version judged an XML file by substrings because its own rows are compared byte for byte. That comparison is right for ownership and wrong for presence. | Ids are counted from the parsed document, the anchor is searched in comment-masked text, and a parse failure is a problem in every mode. One test for each, red before its fix. |
| F13 | MEDIUM (Completeness) | Paperwork a waived issue still owes was missing: the feature doc had no "How to verify in game" section and no waiver note, its older sections still described a Wainrider-only feature, `war-ram.md` and `troop-weight-system.md` did not know the two troops, the feature map had no chariot row, and no test pinned the Gems rows of the ram line. This RCA also claimed a second source line on the 2026-11-14 lesson that was not in the file. | Completion steps left for "after the review" | I wrote the new doc section and stopped at it. The claim in the RCA was written before the edit it described. | All added; `EveryTroopOnTheIronpassRamLineFromTheBreaker_CarriesUpgradeCostAndDailyUpkeep` pins the Gems rows (it fails with the Steel Chariot's row removed). The second source line is now in the lesson. |

## Root cause pattern: a copied row is a claim about every reader

F3, F4, F5, F7 and F9 are one mistake. A new troop was made by copying its neighbour's block, and a new item by
copying its neighbour's row. The copy carried what the neighbour's block holds and nothing of what other files hold
about the neighbour by id: its Gems cost, its party weight, its ratchet rows, its colour family. It also carried an
attribute (`skeleton_scale`) whose effect nobody had traced on this skeleton.

`validate_moduledata.py` passing was read as "the data is complete". It checks references and rules inside the
troop file. The per-id ledgers are read by code and by gates that the validator does not run.

The order of work made it worse: the review was launched before the full suites had finished, so two faults that a
test names in one line were also paid for in review time.

## Why each reviewer missed or caught these

- **Lens 5 Data flow** caught F3 and F7 and saw F4. It did not see F1 or F2 (scripts were not in its files).
- **Lens 7 XML** caught F5, F6 and F7 and confirmed the bytes. It did not open the engine code for the item
  attributes and so did not see F9.
- **Lens 2 Engine** caught F8 and F9. Its question list came from my own doubts, which is why F9 was asked at all.
- **Tooling** caught F1 and F2. It ran a read-only copy of the guard against the live Armory, which is what showed
  the wrong matches.
- **Codex** caught F3 and F5 and found the doc's "two materials" claim wrong (the steel cart mesh has four). It
  left the skeleton scale UNVERIFIED at the native boundary; lens 2 had the decompile.
- **Nobody** caught F4 or F10 before the test suites did.
- **Second wave:** the Tooling re-review caught F11 by running remove and apply on a copy of the live files, which
  my tests did not do. Codex caught F12 with three in-memory edits of the live files. Lens 4 caught F13 and the
  false claim in this file. Lens 3 found nothing (a full check takes 30 ms). Lens 6 proposed four changes: two
  are applied (the shared `package_guid`, a shorter comment beside `DWARF_MOUNT_IDS`), one is not (merging the
  tool's two planners: after F12 the shared logic is in `_state`, and what is left differs by anchor kind), and
  one changes what a player sees and is the maintainer's (a second helmet and bracers for the Steel Chariot).

## Lessons recorded

- `docs/reviews/lessons/data-content-cultures.md`: "A new troop is a row in every per-id ledger of its line".
- `docs/reviews/lessons/build-tooling-workflow.md`: "A guard that looks for a package matches the whole file name,
  and a rollback removes its own bytes", and "A writer's fixture holds what the real document holds, and its
  restore is tested on a copy of the real file".
- `docs/reviews/lessons/animation-skeleton.md`: "A skeleton scale is applied by bone index, so it belongs to one
  skeleton".

## Open

- Decided by the maintainer in the session: `skeleton_scale="empire_horse"` is removed from `taom_chariot_a` too
  (live Armory, backup kept); the Steel Chariot's second set gets a second helmet and bracers; the Erebor emissary
  sells both chariot troops.
- FOLLOW-UP (no issue: same waiver): the Wainrider items `taom_chariot_a`, `taom_chariot_armour_rhun` and
  `taom_chariot_armour_imperial` and the port's other live edits have no repo-held spec. They belong to the port's
  branch `feat/chariot-rig`, which also holds the dwarf rider row snapshot that
  `LiveArmory_DwarfWarriorRows_EqualTheSnapshot` needs.
- In-game checks owed: where a dwarf stands on the cart, the collar and reins in motion, the team colour on a mesh
  with two unmasked materials, the far LODs, the two troops in a battle and on the upgrade screen.
