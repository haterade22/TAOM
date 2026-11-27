# RCA: war chariot rig replacement (deep review and Codex pre-review, 2026-11-24)

## Top line

On 2026-11-23 the war chariot moved to a new rig in the live Armory: one package of clips and skeletons from the
upstream chariot pack, with its Monster, action sets, usage set, sounds and voice file. The repo change records
that rig and makes the repo's checks read it. `/deep-review` at tier 1 (lenses 2, 5, 7 and Tooling) with a Codex
pre-review at `ultra` found 1 HIGH and 7 MEDIUM findings, none of them in the live rig: every rider action set that
a chariot troop can have is complete, every clip and sound resolves, the voice file is registered as Native
registers its own, and the server pack holds both chariot skeletons. The maintainer had run the Rhûn chariot and
both dwarven chariots in battle before the review. No finding reached a player.

The HIGH finding is in the tool that built the rig package. Its option that proves a text absent from the output
could not read compressed data. The live package was read again with the corrected search: no name of the pack,
its author or its carts is in it, in its bytes or in any unpacked segment.

The second wave (lenses 3, 4 and 6, a Tooling re-review and a Codex re-review at `high`) found no HIGH. Codex
found that two of the first fixes still read less than they claimed (F9 and F10), which is the root cause pattern
below a second time, and one silent pass (F11). The Completeness lens found first-rig text presented as current
in five other docs. All of it was corrected before the commit.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Tooling) | `--forbid` of `tools/tpac_slim_package.py` searched the output file's bytes only. LZ4 writes a repeated run as a back reference, and all 182 segments of the rig package are compressed, so 316 strings of that package were outside the search while the tool printed "absent". | A check that claims more than it read | I tested the option with a path in a Texture's metadata, which is never compressed, and I did not ask where else a text can be in a cooked package. | `forbidden_hits` reads the bytes and each unpacked segment, and the message names both. Test with a compressed segment in which a back reference splits the text. Lesson in `build-tooling-workflow.md`. |
| F2 | MEDIUM (Codex P2) | Both asset checks decided "this clip carries `quad_movement`" by a search for that text in the clip's metadata. A clip with the text in another field (a source name) and an empty usage list passed. No live clip is such a clip. | Text search where a record should be read | The first fix of this change moved the search from the file to the clip, and I took "the clip's own metadata" for "the clip's usage list". | `verify_mount_assets.quad_tagged` walks the fields in the engine's order to the usage list; the audit calls it. Before the switch the parsed read and the text search were compared on 534 Armory creature clips and 6,177 vanilla clips: no difference. Lesson in `animation-skeleton.md`. |
| F3 | MEDIUM (Data flow, XML, Engine) | Only the dwarf rider rows of the snapshot's `action_sets.xml` were refreshed. The snapshot's chariot sets, action types, usage set, usage stylesheet and `project.mbproj` were still the first rig's, so 109 of the 132 dwarf chariot rows in the snapshot bound actions that the snapshot did not declare. A restore by the README would have given that state. | A partial refresh of a set of files that restore together | I refreshed what the failing test compared and stopped there. | The five files copied from live (each differed from live only inside its chariot blocks). The README says what a restore still needs from outside the folder. |
| F4 | MEDIUM (Data flow) | No gate read the chariot rider's clips. `verify_mount_assets.py` read the three `as_chariot` sets, which name no rider clip; the audit that reads the rider rows only prints. A rebuild of the rig that dropped one of about 30 rider clips would have passed every gate. | A check scoped to the mount's own sets | The tool's entry said "the rider rows of as_chariot name chariot_rider_* clips". They do not: the human rows come from `action_sets.xslt` and the dwarf rows are in another set. | CHECK 3 reads every action set of the file and the stylesheet for the two rider clip prefixes (30 clips today). The Monster's rein skeleton is checked too, as the gate's note already said. New gate `chariot-body`. |
| F5 | MEDIUM (Codex P2) | The shared package parser read a negative segment count as "no segment" (a loop over `range(-1)`), so a damaged package round-tripped and the slim tool would have written it. | Input not validated at the parser | The round-trip gate compares bytes, and a wrong count that survives a round trip is equal on both sides. | `_read_item` refuses a negative length or count. Test on an item with no segment. |
| F6 | MEDIUM (Tooling) | `verify_mount_assets.py` ended with a traceback and exit 1 when the install was missing, so its gates showed FAIL where they had not run. | NOT RUN reported as FAIL | The tool was written to be run by hand on the desktop. | Exit 2 with an `ERROR:` line, which `data_gates.py` reports as NOT RUN. |
| F7 | MEDIUM, UNVERIFIED (XML, Engine) | The four chariot mount and dismount actions have no action type (vanilla types its own), and the jump rows have two directions and no start action. | Upstream data taken as it is | Both are equal to the pack's own files, compared on 2026-11-24. A battle of troops uses neither, so the maintainer's runs did not exercise them. | Not changed: a type added on a guess could bring its own fault (#1499 was a type on a clip that did not fit it). Checks 8 and 9 of "How to verify in game" in the feature doc. |
| F8 | LOW (several) | The gate note named two skeletons where one was checked. The audit's rider clip pattern missed the map clips. Comments in both tools and the authoring guide said every movement row needs the tag. The feature doc named a physics material the port did not add and called current sections history. A cut-off source ended in a traceback. The printed package id had the wrong byte order. The write followed its existence check by a separate step. | Text ahead of the code | Each was written from memory of the first rig or of an earlier design. | Each corrected with the code beside it. The unproven option `--externalise-textures` is removed: it served an approach that was given up, and its text named a tool that is not in the repo. |

| F9 | MEDIUM (Codex re-review P2) | After the first fix `quad_tagged` walked to the usage count and then searched the rest of the metadata for the text. The text inside another record's numbers passed, and so did a record cut short. | The same fault as F2, one level down | I stopped the walk where the layout got harder (each record kind has its own size) and searched from there. | The records are walked by their sizes (`USAGE_PAYLOAD`, from the animation system map) to the last byte. A walk with these sizes ended exactly at the end of the metadata for all 6,711 live clips. |
| F10 | MEDIUM (Codex re-review P2) | After the first fix `--forbid` unpacked a segment only when its stored and unpacked sizes differ. The Kit packs a segment even when that saves nothing: 139 LZ4 segments with equal sizes were in the Armory's creature folder (none in the rig package). | The same fault as F1, one level down | I took the shared parser's `is_compressed` (a comparison of the two sizes) for the format's own answer. The storage format is a byte of the segment's entry. | `forbidden_hits` tries every segment as LZ4 and refuses one that is marked packed and does not unpack. Test with a 17-byte block that unpacks to 17 bytes. |
| F11 | MEDIUM (Codex re-review P2, Design) | The rider clip check swallowed a missing `action_sets.xslt` and passed on the rows of the other file alone. | A PASS for data the check did not read | An `except ... pass` written for a creature entry that has no stylesheet key. | A configured Monster file or stylesheet that is not there is a FAIL that names the file. It is not exit 2: the install is there, and this is what a reinstall leaves. |
| F12 | LOW (several, second wave) | `--forbid` lowered bytes, which is exact only for ASCII. A failed write or read-back left the file. Five guards and two fixes had no test that could fail (mutants survived). First-rig text stood as current in five other docs, the tool had no catalog row, and the gate tool's help did not name the chariot. | Text and tests behind the code | The first fix pass wrote the fixes and one test each, and did not ask of each test "what wrong code would still pass this". | `--forbid` takes ASCII only; a refused run leaves no file; a test for each surviving mutant; the docs, the `tools/README.md` rows and the help corrected. Three deletions from the Design lens applied (`toc_end`, `new_paths`, a second path locator). |

Not changed, and recorded in the feature doc under "Left as they are in the live files": comments in five live files
that still describe the first rig, eight declared action types that nothing binds, and the collision class declared
in both physics material files (the warg's class has had that shape since August 2026). The game was running during
the fix pass, so no live file was written.

## Root cause pattern: a check that says "absent" or "tagged" for data it did not read

F1, F2, F4, F9, F10 and F11 are one fault. Each check printed a general claim ("text absent", "clip tagged", "no phantom
bindings") after it read a narrower thing than the claim covers: the file's bytes and not its payloads, the text
and not the record, the mount's sets and not the rider's. Each had a test, and each test's fixture held the data in
the one place the check did read. The first rig hid all three: one clip per package, uncompressed metadata, rider
rows that this tool never had to read.

The question that finds this class before a reviewer does: for the claim in the PASS line, where else can the thing
be, and does the fixture put it there?

F9 and F10 show what a half answer costs. Each first fix moved the read one step closer to the data (from the file
to the clip, from the bytes to the segments whose sizes differ) and kept a shortcut at the next step. The format
has records and a storage byte; the second fixes read those.

## Why each reviewer missed or caught these

- **Tooling lens** caught F1 by building a package with the text inside a compressed segment, and F6 by pointing the
  tool at a folder that does not exist. It judged `clip_metas` correct, which it is; the weakness of F2 was in what
  the callers did with the metadata.
- **Codex** caught F2 and F5 with counterexamples built in memory. It could not run the checks that need `xxhash`
  and `lz4` in its own Python, and said so.
- **Data flow lens** caught F3 and F4 by tracing a rider from troop XML to a clip and asking which gate would see
  each link break.
- **XML lens** and **Engine lens** both found F7 and both marked the engine side UNVERIFIED. The XML lens compared
  the live rows with the first rig's; the comparison with the pack's own files was mine, after the reports.
- **Codex, second pass** caught F9, F10 and F11, each with a counterexample that an independent reader of the
  format (TpacTool) confirmed. The **Tooling re-review** ran twenty mutants and named the eight that survived.
  The **Design lens** proposed three deletions and the stylesheet fix; its proposal to report a missing Monster
  file or stylesheet as NOT RUN was applied as a FAIL instead, for the reason in F11. The **Efficiency lens**
  measured the gate at 0.13 s and proposed nothing that passes the simplicity rule. The **Completeness lens**
  found the docs and catalog gaps of F12.
- **The convergence pass** ran 27 mutants on the final code and found nine LOW items, no defect in a fix: three
  tests that could not fail (the storage byte of F10, the record sizes and the zero int of F9, the audit's rider
  rows), an unused import, a function named for less than it returns, and stale text in a catalog row and a test
  docstring. All were corrected, and `--forbid` now refuses an empty text too.
- **The suites** were green before the review (11,315 tool tests; the C# suite with two failures that are not from
  this work), which is why no finding here is a failing test.

## Lessons recorded

- `docs/reviews/lessons/build-tooling-workflow.md`: "A check that reports a text absent from a package reads the
  compressed payloads too".
- `docs/reviews/lessons/animation-skeleton.md`: "A flag read per package is blind when one package holds many
  clips", extended with the usage-list read and the vanilla measure (six of the horse's seven in-place rows).

## Open

- In-game checks 8 to 12 of `docs/features/chariot.md` "How to verify in game": mount and dismount on both sides,
  the jump key, wheel sounds and voices, a chariot death with its rider, a dedicated server start.
- FOLLOW-UP (no issue: the maintainer waived issues for the chariot work): rewrite the stale comments in the five
  live files and remove the eight unbound action types, with the game and the Kit closed, then copy the files into
  the snapshot again.
- FOLLOW-UP (no issue: the maintainer waived issues for the chariot work): a repo-held spec and a re-apply tool for
  the port's live rows and the rig package. Today a reinstall of the Armory removes the chariot, and the gate
  `chariot-body` reports it.
- FOLLOW-UP (no issue: not filed yet, a fault of the shared parser outside the chariot work; the maintainer
  decides): `tpac_clone_metamesh.Segment.is_compressed` compares the two sizes, so `segment_payload` returns the
  packed bytes of an LZ4 segment whose sizes are equal. All 139 such segments found are small import records that
  no tool unpacks today. Three writers and the fixtures of two test files depend on the size rule, so the change
  is its own work.
- FOLLOW-UP (no issue: not filed yet; the elephant's check already fails on trunk for another reason): give the
  elephant entry of `verify_mount_assets.py` its rider clip prefixes, as the chariot has (Design lens).
- The provenance register still lists the chariot's source as uncleared and unnamed; a paragraph of 2026-11-23
  facts was added to it, and its status is the maintainer's to change.
