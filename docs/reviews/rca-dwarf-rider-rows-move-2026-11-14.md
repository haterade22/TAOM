# RCA: moving the dwarf rider rows from TAOM to the Armory (#1226)

Findings from the review of the move: 223 creature mount rider rows left TAOM's `action_sets.xml` (PR #1209) and went into
`as_dwarf_warrior` in the live Armory file and its repo snapshot. The change edits live Armory data, so it ran as tier 1:
Codex at `xhigh` (lowered from `ultra`: a mechanical data move with no production C#), the data flow, engine compatibility
and XML lenses, and no second wave because nothing reached CRITICAL. The XML itself came back clean (byte-identical live
and snapshot, a pure 227 line insertion, no repeated or declared-nowhere type). Every confirmed finding sat in what
surrounded the data: the tests, the release path and the docs.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The fix now reaches players only with an Armory package built after 2026-11-14. The `public`, `testing` and `patreon` Armory copies predate the rows, `tools/package_release.py` reads no Armory content, `/release` had no item for it, and a mistyped channel path made the check skip instead of fail (Lens 5 gap 1; reproduced here on all three channels and on a bad path) | The guard stayed with the old home | The plan named reinstall safety but not the release path. `release-process.md` lists each Armory dependency by hand, and nobody added this one | `release-process.md` "Since #1226" with a check that fails on a missing folder (`--settings TAOM.Tests/binding-gate.runsettings`), `/release` item 6c, and a `Known limitation` paragraph in the commit body |
| 2 | MED | An art drop or reinstall drops the rows and no replay gate notices: `data_gates.py` and `/armory-audit` held no row for them (Lens 5 gap 2) | Gate gap | TAOM's partial sat in a tracked file that a reinstall could not touch, so no gate ever had to guard it | `dwarf-rider-rows` row in `tools/data_gates.py`; one sentence in `/armory-audit` |
| 3 | MED | `LiveArmory_ActionSets_EqualTheSnapshot` compared two 4.5 MB files whole. It fails when another session edits either file, depends on `core.autocrlf`, and would print about 9 MB on failure (Lens 5 inconsistency 3, Lens 7 L1, Codex P3) | A check wider than its class | I wrote it to protect the snapshot copy and did not ask who else edits the file | Replaced by `LiveArmory_DwarfWarriorRows_EqualTheSnapshot`, which compares only the dwarf rows and says which side to refresh |
| 4 | MED | The rewrite dropped the offline coverage of the 101 elephant and chariot rows: with all of them deleted, every offline test still passed (Codex P2-1, Lens 7 L3; Codex mutated the data, so did I) | Coverage lost in a rewrite | I kept the checks whose data the snapshot holds and removed the two elephant and chariot DataRows without a replacement. The snapshot lacks the xslt, so only the live test covered them | `Snapshot_DwarfWarriorSet_BindsTheCreatureFamilyRows` pins six family counts (223 rows), and the two DataRows are back |
| 5 | MED | The release check passed a conflicting duplicate: a second `act_warg_forward_walk` bound to another clip still satisfied `IsSubsetOf`, while native keeps the last row of a type (Codex P2-2, Lens 5 L6) | A presence check where the engine decides by position | `IsSubsetOf` reads as "present"; the engine's rule is "last row wins" | `AssertEachBoundToItsClip` asserts each expected type has exactly one clip; the expected side must be non-empty |
| 6 | LOW | The female set test forbade a harmless guard copy, and its regex missed 6 of the 223 types. It also never checked the order that native inheritance needs (Codex P3, Lens 7 L2) | An over-pin | I pinned the design choice, not the behavior | `Snapshot_FemaleDwarfSet_InheritsFromTheMaleSetDeclaredBeforeIt` checks `base_set` and the document order |
| 7 | LOW | Stale statements: `war-ram.md` step 11 told the reader to delete a guard that the change had deleted; the snapshot README still named TAOM's dwarf block as an intended merge and had no 2026-11-14 log entry; `bannerlord-animation-system-map.md`, the `test_audit_mbproj_registration.py` docstring and `lotrlome-dwarf-attack-changes.md` kept old claims; `war-ram.md` vouched for counts (203, 4,843) that never matched the file; `release-process.md` stated the crash cause as fact (Lens 5 inconsistency 5, Lens 2 L1 to L3, Lens 7 L4 and L5, Codex P3) | Docs behind the change | My first stale-reference grep hit its 30 line cap inside `.tmp/` worktree copies, and my second searched for the old names, not for the old claims ("guard", "can be deleted", "at the end of") | All fixed. A move greps the old home's claims as well as its names |
| 8 | INFO | Trunk and sibling worktrees still carry PR #1209's tests. Two of them (`..HaveNoRiderRowsOfTheirOwn`, `..BindsNoTypeThatTaomsRowsAlsoBind`) fail against the edited live Armory (Lens 5 inconsistency 4; inferred from the assertions, not run) | A shared unversioned file edited from one worktree | The live Armory is one file for every checkout | Commit and push soon; the commit body says so |

## Disputed or not applied

- Codex suspect 5: `tools/Generate-ActionSets.ps1` would drop the rows. Lens 5 said the script never touches the Armory. I read its header and parameters: it writes TAOM's `Main/_Module/ModuleData/races/action_sets.xml` from Native and an old mod copy, and `project.mbproj` registers only the root file. Not applied.
- A replay writer with `--check` for the 223 rows (Lens 5 gap 2, optional). FOLLOW-UP no issue: it would repeat a derivation that `DwarfMountRiderRowsTests` already pins, and it is the maintainer's call after the next art drop.
- The snapshot restore recipe leaves out `action_types.xml` (Lens 5 L7). FOLLOW-UP no issue: it was so before #1226, and a restore of `action_sets.xml` alone leaves the live `action_types.xml`, which declares the types, in place.
- `bannerlord-animation-system-map.md` line 639 assumes the BUTR launcher order, while the vanilla launcher can list TAOM above the Armory (Lens 2 L4). FOLLOW-UP no issue: it predates #1226 and does not touch the dwarf rows, which now sit in the Armory's own element.
- Lens 2's five UNVERIFIED points (the native consumer of `monster_usage_mounting`, the spider rider's mount and fall actions, the vanilla launcher order, clip preloading, the managed callback address). FOLLOW-UP no issue: none changes the move, and the in-game steps 10 to 12 in `war-ram.md` are still owed.
- Lenses 1, 3, 4 and 6 were SKIPPED BY TIER, so Step 4 (improvements) does not apply.

## Root-cause pattern

Findings 1 to 5 share one cause: **a fix moved to a new home, and the guards of the old home did not move with it.** In TAOM's tracked file the rows were safe from a reinstall, covered by a test that read that file, and shipped with every TAOM build. In the Armory none of that holds. The data was moved correctly, and the review found that the tests, the gates and the release path still described the old home. Findings 4 and 5 add a second cause: I rewrote a test around the data I could see in the snapshot and checked that it passed, but I did not run it against bad data. The mutation runs after the review (101 rows deleted, one conflicting row appended) now fail 2 of 2 live tests and 9 of 15 offline tests, and the data was restored byte for byte (sha256 prefix `45b9a97e7aad`).

## Why each agent found what it found

- Codex was the only reviewer that mutated the data, so it alone found 4 and 5 by running them. It also disputed the female guard on native evidence, and Lens 2 confirmed that with its own decompile.
- Lens 5 traced the rows to the player, which is why it alone found the release hop (1), the missing replay gate (2) and the sibling worktrees (8).
- Lens 7 ran the gates and compared bytes. It found the lost DataRows (4) and the test precision items (6), and it confirmed the XML.
- Lens 2 found no incompatibility. Its value was the native evidence for the female set: `FUN_180591470` copies the base when it parses the child, in document order.

## Feedback memories to codify

None. The lesson goes to `docs/reviews/lessons/build-tooling-workflow.md`.
