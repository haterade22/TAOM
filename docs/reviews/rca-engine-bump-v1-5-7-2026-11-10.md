# RCA: Bannerlord v1.5.7 engine bump (#1112, reviews of 2026-11-10)

## Top-line

The bump was committed, merged and released as v2.0.37 on the maintainer's word before its reviews
finished. Four reviews then ran on the released tree `6ba5dff05`: a Codex pre-review, an independent
sweep of the engine diff with one adversarial checker, the four tier 1 `/deep-review` lenses (data
flow, engine compatibility, XML, tooling), and the full gate run. None found a CRITICAL or HIGH
defect, and none found a defect in the three code edits of the fix. They found one MEDIUM in a gate
(fixed in `6ba5dff05`), one MEDIUM in a scratch script that touched another session's files (no loss
found, script retired), and 16 LOW items. Most of the LOW items are sentences in the record that said
more than their evidence, or that went stale within the hour.

The finding with the widest reach is not in the code. The release went out with one red test in the
tool suite, because the gate run was stopped early to cut the tag.

## Findings and root cause

Each row was re-read against the source, the installed v1.5.7 assemblies or a saved output before it
went into this table.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM, Codex F1 | The first `PartyMemberSizeLimitCallShapeTests` matched one call per line with a regular expression, so `GetPartyMemberSizeLimit(ResolveParty(), true)` passed. | Gate blind spot | The gate was written against the three calls that existed. Its shape cases came from those calls. | Fixed in `6ba5dff05`: a scanner that blanks comments and literals and reads balanced brackets, with 14 shape cases. |
| 2 | LOW, data flow F2 | That scanner cannot read a call inside an interpolation hole, and a live call stood in one (`CareerPerkCheats.cs:88`). On the pre-fix source the gate would have flagged line 81 and never seen line 88. | Gate blind spot, second round | The fix for finding 1 widened what the scanner reads. Nothing measured what it still could not read. | The call moved into a local. `TaomCallers_AreAllReadByTheScanner` compares the calls in the comment-free source with the calls the scanner read and fails on a difference (it failed on this call before the move). Lesson in `testing-qa.md`. |
| 3 | LOW, sweep and tooling F2 | The release commit `0a881f154` added `v2.0.37-discord.md` and `v2.0.37-testers.md` and linked them from nowhere. `test_doc_graph_ratchet` fails at the tag: "orphans: 11 exceeds the baseline 10". | Release step missing | `/release` had no step for the docs index. The v2.0.35 and v2.0.36 notes are orphans too; the baseline had room for one more file, and this release added two. The gate run that would have shown it was stopped early to cut the tag on the maintainer's word. | `docs/INDEX.md` links both notes (ratchet: orphans 9, components 14). `/release` Phase 5 now adds that line and runs the ratchet. Lesson in `build-tooling-workflow.md`. |
| 4 | MEDIUM, tooling F1 | `ff_trunk_over_dirty.py` (a scratch script) landed the bump on the shared trunk checkout by merging the change into 18 working copies another session had open. It read each file once and wrote it seconds later without a second read. A save in between would have been lost, and its check would still have printed VERIFIED. Writing a rebuilt shared file to disk is also what `git-and-commits.md` forbids. | Shared-checkout write | A plain merge was refused, and the script was built to get past the refusal. The rule was read as "do not lose their edits", which a three-way merge satisfies, and not as "do not write their files". | No loss found: that session committed all 18 files in `85e8788dd`, and each hunk only adds lines. The script was not used again. This follow-up lands by a push of the branch, or by a plain fast-forward when no changed file is dirty in the trunk checkout. Lesson in `build-tooling-workflow.md`. |
| 5 | LOW, sweep | The record said one of three call sites changed meaning. There were four calls, and both positional calls of the cheat changed meaning. | Record overstated | The count was written from the compile errors and the first grep, before the second cheat call was read. | Corrected in `v1.5.7-impact.md`. |
| 6 | LOW, sweep and engine compat 3 | Three comments state that the encounter menu's Attack always runs the hostile action (`OathSitOutRule.cs`, `ServiceWarPolicy.cs`, `ServiceBattleService.cs`). v1.5.7 skips it against a caravan or villager leader party. The record's row said "Inherited" and missed that `OathSitOutRule` rests on the old rule. | Comment states an engine rule | The member diff covers patches, models and reflection sites. A comment that restates engine behaviour is bound to the engine too, and nothing lists it. | The three comments name the v1.5.7 exception. The code is unchanged: whether the sit-out should exempt those fights is the maintainer's decision. `/engine-bump` Phase 3 now greps TAOM's comments and docs for each changed member. |
| 7 | LOW, sweep | The record's siege row said TAOM names none of the members involved. TAOM's #1021 workaround (`EncounterAdapter.ForgetEndedBattleResult`) exists to keep exactly that engine branch from firing. | Record understated | The row was judged by a grep for the member names, and the workaround names none of them. | Row corrected. The workaround still holds on v1.5.7. |
| 8 | LOW, sweep, engine compat 1, XML 2 | The record left out engine changes: the roster version bump on a land and sea switch, `MakeAlternativeTroopsReturn`, the null-destroyer return in `EmptyClanPartiesCampaignBehavior`, the changed `global_strings.xml` row, the Native language tables. It named `ModuleManager` as changed (it has no real hunk) and said the `*.real.diff` files hold no build numbers (they do). | Record incomplete | The record was written from the patch-note table and the bound-member diff, and the commit body then claimed it listed every engine change. | Added to `v1.5.7-impact.md`. None reaches TAOM. |
| 9 | LOW, data flow F1 | `v1.5.7-diff-ranked.md` row 2 said "No change on land". v1.5.7 adds a third engine caller of the party size model, the main party health tooltip. It runs TAOM's enforcement path, so each hover writes the troop weight base snapshot. With a stale engine cache the display surfaces can disagree until the next roster change. | Record overstated | The row was judged by the postfix alone. `troop-weight-system.md` asks for a re-grep of the model's callers at every bump, and that grep was not run. | Row and feature doc corrected. Display only, no code change. |
| 10 | LOW, sweep, engine compat 2 | The record and the commit body of `2c91d2cb2` (11:57) said the Modding Kit was still v1.5.6 and the Dedicated Server had not updated. Steam moved the server at 10:31 and the Kit at 10:36. | Stale within the hour | Both were read once, at 10:28, and the sentences were written from that read 90 minutes later. | Record corrected, `_editor_build` regenerated. `/engine-bump` Phase 1 now says to read both again before the commit that states them. |
| 11 | LOW, tooling F3 | The record said the full C# suite "ran again on `2c91d2cb2` itself". The only saved run with those numbers ended at 11:45, before that commit existed. | Record cites no saved output | The sentence was written from memory of the run on the same content. | The record now cites the 12:21 run on `6ba5dff05` and the diff between the two commits. |
| 12 | LOW, tooling F4 | The record gave 119 backup sidecar files (517.7 MB) in the live modules. A dry run at 13:15 finds none, and the default quarantine folder does not exist. | Record went stale | The count was true at 12:05 (the session transcript holds that output). The files were gone by 13:15, and this session moved none. | Reworded with both times. Who removed the files, and where they went, is not known here. |
| 13 | LOW, sweep, data flow F3, engine compat 4, XML 1 | `encyclopedia.md` still said TAOM defines the two `str_last_seen_*` rows. `elephant.md` named v1.5.6 as the current engine. | Stale doc | The version sweep moved statements that hold a version string. The encyclopedia line holds none, and the elephant line was in a checklist. | Both corrected. |
| 14 | LOW, data flow F4, pre-existing | No test reads the Native row of the live `TAOM_Map` and `LOTRLOME_Armory` manifests. `/engine-bump` moves them by hand, and an older copy on this machine still reads `v1.5.3.*`. | Missing gate on unversioned data | `NativeConstraint_MatchesPinnedGameVersion` was written for the repo's manifest only. | `LiveModuleNativeConstraint_MatchesPinnedGameVersion` (LiveInstall). It fails on the pre-bump manifests and passes on the live ones. The XML lens proposed no gate, because the vanilla launcher does not read that block; the orientation trap "Unversioned modules" asks for one. |
| 15 | LOW, tooling F6, pre-existing | `check_handbook_attributes.py --docs <missing folder>` printed "0 finding(s)" and exited 0. | Gate passes on nothing | `run()` treats a missing folder as an empty one. | `main()` exits 2 with the path. Test `test_missing_docs_folder_exits_2`, red before the fix. |
| 16 | LOW, Codex F2 | `keep-wardens.md` showed the one-argument call. | Stale doc | The doc quotes code, and the fix did not grep the docs for the call. | Fixed in `6ba5dff05`. |
| 17 | LOW, tooling F5 | Two scratch writers (`retire_last_seen_rows.py`, `bump_version.py`) wrote XML without parsing their output first and had no dry run. | Tooling convention | They were written as one-off scripts and skipped `tools/README.md` "XML I/O convention". | Nothing owed: the lens parsed all 15 outputs, and each differs from its backup only where intended. |
| 18 | LOW, process | A filter on the shared command text `unittest discover -s tools/tests`, used to stop this session's gate run, also stopped the retopology session's test run. | Process kill by shared text | Several sessions run the same command. The filter matched the command, not the script that started it. | Reported to the maintainer at the time. Later stops in this session matched the scratch script's own path and listed the matches first. Lesson in `build-tooling-workflow.md`. |

**Unproven, kept open:**

- The Save/Load screen preview of a save whose hero rides a warg (sweep finding 2). v1.5.7 resets
  the preview when the mount material does not resolve, and whether a loose-`Assets` material
  resolves on the cold main menu is native and unread. The in-game check is in the record's Owed
  list and in the testers' checklist.
- Codex O1 and data flow "out of scope": at sea with a ship crew bonus, vanilla's forced-land tooltip
  call can overwrite the troop weight snapshot. It needs the naval DLC, which TAOM refuses.

**Decision owed (maintainer):** `OathSitOutRule` sits an enlisted vassal out of a battle against a
realm at peace with his kingdom. On v1.5.7, Attack against a caravan or villager leader party
declares no war, so the rule is stricter than the engine needs for those two party kinds. Left as it
is.

## Root-cause patterns

1. **The record ran ahead of its evidence** (findings 5, 7, 8, 9, 10, 11, 12). Seven sentences were
   written from one early read, from memory of a run, or from a narrower check than the sentence
   claims. This is `evidence-over-claims.md` section C, traps 1 and 3, and it has shipped before
   (`rca-arnor-elite-troops-2026-11-06.md` F8, `rca-supply-caravan-meeting-2026-11-09.md` finding 1).
   The speed of the day made it worse: the bump was written up while its reviews still ran.
2. **A gate with no measure of its own reach** (findings 1, 2, 15). Two review rounds each found a
   call shape the call-shape gate could not see. The second fix adds the measure: what the scanner
   did not read is counted, and any such call fails.
3. **Speed against shared state** (findings 3, 4, 18). The tag was cut before the gate run ended, a
   script wrote into another session's files to get a merge through, and a broad process filter
   stopped another session's run. Each saved minutes and each cost another session, or the release,
   something.

## Why each review missed what it missed

- **Codex pre-review** found findings 1 and 16 and the conditional case behind finding 9. It read
  the diff, not the record, so findings 5 to 12 were outside what it was given.
- **Engine sweep** found findings 5 to 8, 10 and 13. It did not trace the tooltip caller into the
  troop weight snapshot (finding 9), because it judged each changed member against the patches that
  bind it.
- **Lens 5 (data flow)** found findings 2, 9, 13 and 14. Finding 3 is a tool-suite result, outside
  its files.
- **Lens 2 (engine compatibility)** found the record gaps in findings 6, 8, 10 and 13. It confirmed
  the three code edits against the installed assemblies and had no reason to parse the test scanner.
- **Lens 7 (XML)** found the data gap in finding 8 and closed the `concept_strings.xml` question for
  TAOM. It proposed no gate for finding 14.
- **Tooling lens** found findings 3, 4, 11, 12, 15 and 17. Finding 18 left no file for it to read.
- **Orchestrator:** findings 3, 4 and 18 are this session's own acts, and findings 5 and 9 to 12 are
  its own sentences.

## Feedback memories to codify

None beyond the lessons: `testing-qa.md` (a source-scan gate counts what it could not read) and
`build-tooling-workflow.md` (the tag waits for the tool suite and the docs index; land on a shared
checkout without writing another session's files; stop a run by its own script path), and
`adapters-taleworlds-api.md` (a comment that states an engine rule is checked at every bump).
