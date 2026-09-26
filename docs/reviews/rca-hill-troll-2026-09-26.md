# RCA: hill troll swing crash, formation spacing, hand morphs and war hammer (2026-09-26)

**Scope:** the second day of the hill troll on `troll_skeleton_a`. Code: Mike's `25995dc9` (07:20: Patch92 formation
spacing, the Custom Battle banner model, a temporary action trace) and `30abf550` (15:48: the review fixes, the tools,
the docs and the lessons below). Data: the live, unversioned `LOTRLOME_Armory` (the standalone action set, the
Monster, the clip packages, the hand-morphed FBX, the war hammer). Uncommitted when this was written and left to the
final review: the clip generator's key fix (`tools/gen_troll_anim_clips.ps1`, `tools/tests/test_gen_troll_anim_clips.py`)
and a Brute Force target cap (`Main/Features/TrollBruteForce/`). Reviewed twice by `/deep-review`: a first review in
the morning (eight lenses, reports 10:01 to 11:08, then fix streams) and a convergence review in the afternoon
(seven lenses and two tooling lenses, reports at 14:08, then four fix lanes and a checker, 14:14 to 14:41). The
previous day's RCA: [rca-hill-troll-and-loc-sweep-2026-09-25.md](rca-hill-troll-and-loc-sweep-2026-09-25.md).

## Summary

The swing CTD was ours. `tools/gen_troll_anim_clips.ps1` blanks the clip field the Modding Kit calls "Blends with
animation" (TpacTool: `UnknownClipName`) on every clip it clones; the line came into the clone-by-name path in
`9354b0b2` (2026-09-25), copied from the cave troll path (`4f7ae42e`, 2026-09-18), where it did no harm because the
cave troll never binds its own clips to attack codes. That field keys the engine's native melee attack table. The
hill troll's standalone set bound its release and blocked codes to those keyless clips, the table had no row for
them, and the first swing read through a null row at `TaleWorlds.Native.dll` +0x6590B9. `bind_hill_troll_action_set.py`
rule 0 now keeps those four families on a vanilla clip or a self-keyed troll clip; 30 troll clips are self-keyed.

The first review found two HIGHs (a torn wrist seam in the new hand morphs, and PatchShield's per-call finalizer on
all five Patch92 targets) and a run of MEDIUMs that were mostly claims without evidence behind them and gates that
did not exist. The convergence review found one HIGH the first could not have seen (the war hammer, added at 11:40,
priced at 23 Blunt and swing speed 12 against the cave mace's 86 and 28), and several defects in the first review's
own fixes: a seam gate that reads after the pin and so cannot fail, a morph gate that counts channels but not their
weights and exited 0 when it had checked nothing, and a required `--clips-index` that nothing compared with the disk.
Six incidents fall outside both reviews: a CRLF splice in three live files, validators reading a stale cooked tree,
a live clip package overwritten by a probe script, a fix lane refusing its brief, Blender's default shape key weight
shipping as `DeformPercent`, and a commit record that does not describe the work. Four themes run through all of it.

**Still owed:** the in-game checks (the troll's own swings and blocked recoils, the hammer's tooltip against the cave
mace, deployment spacing in a battle that opens on the deployment screen, a mixed formation losing its bearer); a raw
seam gate before the pin; a gate on shape-key weights and a face re-run at weight 0; the melee gate for every action
set; the hammer's translations (#679); the `Main/SubModule.cs` Patch92 comment (single-owner file). The issues are
filed and closed (#683 to #687) and #385 has its comment. The final review's own list is at the end.

## Findings, first review (2026-09-26 morning)

Eight lenses on `25995dc9` plus the working tree: XML, standards, data flow, engine compatibility, tooling,
completeness, efficiency, design.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| A1 | HIGH | The first hand-morph transfer tore the troll's wrist. Its reference, the Isengard uruk's `SK_Uruk_Hai_BM_A_Arms`, carries a hand region that runs up the forearm, so 25 of the 26 channels moved the 40 wrist-seam vertices stitched to `hill_troll_a_body` by up to 0.089 (42 to 63% of the wrist ring's radius; 16% of the channel peak), while artist hands keep their seam under 2%. The tool reported `ok: true` | Tool: wrong reference, unmeasured seam | The preview rendered only the right hand, with the body hidden; nothing measured the vertices the hand mesh shares with the body | Redone live 11:45:28 from the pale uruk's `SK_Pale_Uruk_BM_A_Hand`: the seam pinned over 3 rings, a 2% seam gate, exactly 26 channels, a palm mirror check, previews of both hands. Kit re-import 12:01:24; the wrist "way better" in game at 12:18 (Mike). The seam gate as built cannot fail (B4). Lessons in `animation-skeleton.md` |
| A2 | HIGH | PatchShield's second pass (`OnGameInitializationFinished`) attached its `__originalMethod` finalizer to all five Patch92 targets (the `UnitDiameter` getter, the three `GetUnitPositionWithIndexAccordingToNewOrder` overloads, `GetUnitSpawnFrameWithIndex`): a `MethodBase.GetMethodFromHandle`, which allocates and takes a reflection-cache lock, on every per-unit call, trolls or not. The pass-2 count in `diag.log` went from 138 attached methods to 139, then 143. The time cost was never measured | Harmony overhead; repeat of #331 | The own-assembly skip reads the target's assembly (`TaleWorlds.MountAndBlade`), not the patch owner's; the Patch38 precedent was recorded only in a `PatchShieldPolicy` comment, and the #331 lessons frame the tax as other mods' patches | `PatchShieldPolicy.ExcludedTargetMethods` and `IsExcludedTargetMethod`, checked in `PatchShield.IsExcludedTarget`; `Patch92BindingTests.EveryPatch92Target_IsOnPatchShieldsHotMethodList` walks the real targets (added after the convergence lenses 1, 2, 4 and 6 found the list tied to nothing); lesson in `harmony-il.md`, which `harmony-patches.md` makes mandatory reading |
| A3 | MED | Rule 0 started too narrow. After the first swing crash it moved only the releases to vanilla clips (2026-09-25 18:08, backup `-180817`); a blocked attack crashed again, so it was widened to the whole melee exchange (08:31, `-083155`), then narrowed to the four table families (09:15, `-091511`) once the table's builder was read | Scope guessed from crashes | The native code that fills the table was read only in the third round; the first rounds inferred the scope from which codes had crashed | Rule 0 (`MELEE_TABLE`, 618 codes, tested); `native_crash_triage.py --dump` prints the faulting thread's registers; lesson "A native hash-map miss names its missing key in a register" (`build-tooling-workflow.md`): find the code that fills a table before widening a data rule |
| A4 | MED | The docs said the 08:41 smoke (275 swings, 117 deaths, no crash) proved the fix. It ran on the widened rule and the old 0.82 capsule: the capsule changed at 08:55 and the narrowing landed at 09:15, and the narrowing put 103 codes (wind-ups, defends, guards, kicks) back on troll clips | Stale evidence | The claim was written from the smoke's result, never set against the write times of the files it covered | `troll-race.md` and the ledger's "Open" say which data each smoke ran on. The final-data smoke ran 12:18 to 12:25 (`rgl_log_76280`): no crash, spacing right, hammer in hand, no troll banners (Mike). Rule proposed below |
| A5 | MED | The spacing tracker re-deployed trolls through `DeploymentHandler.OrderController_OnOrderIssued_Aux(Move)`. That hands `Formation.SetPositioning` the position and facing the formation already holds, which returns without a frame change (`Formation.cs:870, 892, 904`), so the unit snap landed on the human-width slot cache; a formation holding still after deployment also kept human width | Engine behaviour: slot cache | Each spacing fix was checked against the log line it changed (the width), not against where the agents stood | On a width change the tracker calls `OnUnitAddedOrRemoved()` and `OnFormationFrameChanged(updateCachedOrderedLocalPositions: true)`, and while `IsTeleportingAgents` is on it replays the tail of `OnMassUnitTransferEnd`; the re-deploy queue is gone, and the `[TrollSpacing]` line logs `teleporting` again. UNVERIFIED in game: the teleport replay. Lesson in `adapters-taleworlds-api.md` |
| A6 | MED | Trolls could pick up a dropped banner: `BannerBearerLogic.FindBestSearcherForBanner` asks only `CanAgentPickUpAnyBanner`, which is race-blind and which neither TAOM model overrode | Gate scope | The race gate was written for bearer selection; the pickup is a separate engine question nobody listed | Both models override it as `base.CanAgentPickUpAnyBanner(agent) && _service.PassesRaceGate(race)`, and `PassesRaceGate` is tested. The convergence engine lens found a second path (`HumanAIComponent.ItemPickupTick` with `DefaultItemPickupModel`) that never asks the gate and stays shut only because no troll kit carries a shield or a consumable; documented in `banner-bearers.md` |
| A7 | MED | `bind_hill_troll_action_set.py` took `--clips-index` as optional (`default=[]` at `4ceae903`); a run without it would have unbound 374 retargeted troll clips, and `wire_hill_troll_race.py --check` would still pass | Unsafe default; repeat of the 2026-09-25 RCA's "the safe path was not the default path" | The live runs always passed the flag | `--clips-index` required. The convergence review found that a required index matching nothing still passed (B8) |
| A8 | MED | Two width reads Patch92 does not cover: the custom-width order preview measures its copy outside the scope (`OrderController.cs:1287`), and the deployment plan sizes each formation's spawn area with the static `Formation.GetDefaultUnitDiameter` (`DefaultDeploymentPlan.cs:593-607`), so a widened troll formation can overlap its neighbours (the data-flow lens estimated 9 to 10 m per side) | Engine coverage | The patch was scoped to the paths where trolls were seen touching: the slots and the spawn frames | Documented limitations in `troll-brute-force.md` and the Patch92 registry entry; neither effect has been seen in game |
| A9 | MED | No GitHub issue covered any of the work: Patch92, the Custom Battle banners, the swing CTD, the face-morph CTD, the race itself, the troll's own swings | Process | The work ran as crash triage and live fixes, and the completion workflow never ran; the one troll issue, #649, calls `hill_troll` out of scope | Mike approved six issues and a #385 comment; #679 (the hammer's names) is filed, the rest are drafted |
| A10 | MED | "Melee is engine pose-blend" was still stated as fact in ten places, and `troll-race.md`'s recipe step still told authors to bind every human action to the troll clip, the binding that crashed | Claim not swept; repeat | The correction edited the two docs the new lesson named, while the claim lived in ten. Same shape as "When a fact moves, grep every statement of it" (`build-tooling-workflow.md`, 2026-09-24) and the 2026-09-25 RCA's finding 5 | Each place corrected; a grep for "pose-blend" now finds it only as refuted (`troll-race.md`, the clip-flags reference, the asset pipeline, the ledger, `bind_troll_action_set.py`) |
| A11 | MED | #385 (the female dwarf head's face-morph CTD; OPEN, labelled `triage-blocked-external`, last comment 2026-08-09 saying the fix was not applied) was stale: `add_face_morph_channels.py` had been applied to `sk_dwarf_bm_f1.fbx` on 2026-09-25, recorded only in the tool's docstring | Process | The dwarf fix came as a side effect of the troll's face crash | A comment is drafted: the tool, the backup (now in the 09:53 sweep folder), the Erebor in-game check still owed, and the label to `triage-needs-ingame`. The labels were unchanged on 2026-09-26 |
| A12 | MED | Nothing would notice a reinstall or an `export_rig_for_kit.py` re-export dropping the face or hand channels | Missing gate; repeat of the trap "Unversioned modules" | The trap row says to land an in-repo gate with a live fix; the morph fixes shipped without one | `tools/check_race_morph_channels.py` (hill troll head, eye and mouth 101, hands 26; the dwarf f1's head, eye, mouth and arms) and its row in `moduledata-validation.md`. The convergence review found it counts channels, not weights (B5), and passed on SKIPPED (B6) |
| A13 | MED | No live check for the swing crash: `wire_hill_troll_race.py --check` never looked at release or blocked codes, and the snapshot at `4ceae903` still held 64 of them on troll clips, so a restore by the ledger's redo steps would bring the crash back with every check green (XML, data-flow and design lenses) | Missing gate | The rule lived only in the binder | `--check` fails an unkeyed troll clip on those codes (on 2026-09-26 it reports 30 self-keyed, and passes); the snapshot was re-copied from live. It covers only `as_hill_troll_*` sets and keys on the clip name (B11) |
| A14 | MED | `package_release.py` treats `Assets/Race Test/` as a candidate for exclusion, and its usage example passes `--exclude-candidate RACE_TEST`; a release built that way ships XML naming the hill troll's clips and skeleton without them (XML lens; predates this work) | Release packaging | The folder name reads as scratch | OPEN: `tools/package_release.py:32` and `:273-276` unchanged on 2026-09-26 |
| A15 | LOW | Standards, data flow and tooling: an empty catch in the Patch92 postfix; the width store untested inside the patch file; `gamemodels.md` counts nothing computes; dated narrative in the crash-triage skill; a stale `tools/README.md` row. Two tooling items were MEDIUM: the morph tools' round trip missed new, moved or re-boned objects, and the target's two hands were never cross-checked (only the right one was previewed) | Mixed | | The catch removed; the store moved to `TrollFormationSpacingStore.cs` with its own tests and `ReferenceIdentity` keys; the counts dropped; the skill trimmed; one round-trip check, `add_mesh_lods.compare(rekeyed=)`, in both morph tools; previews of both hands |

## Findings, convergence review (2026-09-26 afternoon)

Nine reports on the fixed tree: lenses 1 to 7 and tooling lenses A (Blender and FBX) and B (data). Lens 3
(efficiency) found no defect.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| B1 | HIGH | The war hammer, cloned from the cave troll's mace pieces, priced at 23 Blunt and swing speed 12 against the mace's 86 and 28 (`tools/melee_catalogue.price`, a port of v1.5.3 `Crafting`): the longer haft moved the inertia around the shoulder from 12.19 to 16.81, which floors both torque layers in `CalculateSwingSpeed`. The tool and the README said the damage stayed "the proven ones" | Clone keeps state; repeat | The copy read as a mesh swap, so nobody priced it; the melee ladder gate exempts `hill_troll` with a reason that named the cave mace, true until the roster took the hammer | Head `weight` 1.23 to 0.875 (14:07, `.bak-hammerweight-20260926-140745`): 86 Blunt, speed 28, reach 3.40 m (the mace: 3.08 m). The tool writes `HEAD_WEIGHT`, so a re-run keeps it; the exemption reason names the hammer; lesson in `data-content-cultures.md`. OWED: the tooltip in game |
| B2 | MED | The hammer's three names exist in English only; `check_external_loc_coverage.py` reports +3 untranslated in all 12 languages, and its fix hint names `--module TAOM_Map` for an Armory regression | Localization | The item tool writes English rows only, and no step ran the translator | #679 filed. The gate's result and the hint were unchanged when run on 2026-09-26 |
| B3 | MED | Dead attributes cloned from the cave head (`distance_to_next_piece`, `distance_to_previous_piece`, `stack_amount`) and a wrong mechanism in three docs: "distance 0, so its centre sits on the top of the handle". With `length` present the engine sets both distances to half of it (`CraftingPiece.cs:165-179`), so the head's base sits flush on the handle end | Clone keeps claims | The sentence was written from the attribute's name, not the engine's use of it | The ledger, `troll-race.md` and the README row corrected; the attributes stay (no runtime effect) |
| B4 | MED | The seam gate A1's fix added can never fail. The pin gives every moving seam vertex weight 0 and only moving vertices are written; the gate then reads the seam back from the written keys. Fed the torn run's numbers (seam 0.089, peak 0.552), it passed. "Seam max 0.0 against the limit 0.01035" was quoted in three docs as proof | Gate after the step that masks it | The first review's prescription (hold the seam, fail above 2%) set no order; the tool's lesson said "gate it from the keys actually written", which emptied the gate once the pin ran first; the gate's unit test fed the gate function numbers instead of running the pipeline on a bad input | Docs and lessons corrected ("A gate that runs after the fix it checks can never fail", `animation-skeleton.md`). OWED: a raw seam gate before the pin and a check that the pin held (`transfer_hand_morphs.py:538-539` still reads the seam after writing). Today only `--preview` and Mike's eye guard a wrong reference |
| B5 | MED | Nothing checks shape-key weights. The hands sit at `DeformPercent` 0, but the troll's head, eye and mouth channels (101 each) and the dwarf's tool-added `.eye` sit at 100; the torn backup passes both `check_race_morph_channels.py` and `audit_fbx_lods.py --diff` | Gate cannot see the fault | The weight fix (C5) went into the writers; no reader was taught the field | OWED: record and gate the weights at every layer, a face re-run at weight 0, a Kit re-import. The face channels hold no offsets, so the geometry is unaffected; whether the engine applies `DeformPercent` at all is UNVERIFIED |
| B6 | MED | `check_race_morph_channels.py` printed SKIPPED and exited 0 when the Armory or an FBX was missing, and a truncated FBX raised an uncaught `struct.error` | Exit code; repeat | The printed text was honest and was read in place of the status; "a gate that cannot run has not passed" is a rule, and the exit code did not enforce it | Exit 2 on SKIPPED; `struct.error` and `zlib.error` count as unreadable; the tests pin 2 |
| B7 | MED | The hammer tool refused `--apply` until the package held its three names, tested with `name.encode() in blob`. The head's name is inside the body's (`bo_wm_hill_troll_2h_hammer_head`), so a package holding only a mistyped body (`bo_..._head_a`) plus the handle passed; a `body_name` no package ships is the #352 preload hang | Substring check; repeat | `moduledata-validation.md` already requires exact tokens, worded for deriving a target set; a presence check on a binary package did not read as the same question | `validate_mesh_refs.scan_tpac_metameshes`, by exact name (`add_hill_troll_hammer_items.py:183`); a near-miss test; lesson in `build-tooling-workflow.md` |
| B8 | MED | A7's fix made `--clips-index` required, but an index matching no clip on disk planned 0 retargeted bindings and exited 0, and `--check` still passed | Fix stopped short | The fix answered "the flag is missing", not "the flag names nothing" | The binder refuses when no listed clip is on disk (`bind_hill_troll_action_set.py:280`) |
| B9 | LOW | `validate_moduledata._loaded_tpacs` rebuilt paths as `<parent>/Modules`, so a lowercase `modules` path raised `COLLISION_BODY_BORROWED` | Path handling | Tested with the default path only | `validate_mesh_refs.module_tpacs`, shared by both callers |
| B10 | LOW | Standards (lens 1): the morph gate's rule row claimed every race rig (it checks two FBX); a `PassesRaceGate` comment named a call that does not exist; a "follow-up" note no issue tracked; hand counts in `gamemodels.md`; the skill's dated narrative and a pointer to deleted code; the 1.6 m mounted width stated without its dismount condition; two test names and one test that re-implemented what it tested | Standards | | Fixed in the C# and docs lanes; the `AgentAdapterCache` comparer dedupe is a follow-up |
| B11 | LOW | Design and tooling: the generator should write the key rather than blank it (lens 6 F1); the melee gate covers only `as_hill_troll_*` and keys on the `anim_` prefix and the code family, not on whether the clip has a row (lens 6 F2, lens 4 L6, lens 7 L3, tooling-B F5); two register readers in `native_crash_triage.py` with different guards (lens 6 A3, tooling-B F7); stale help text (lens 6 A2, tooling-B F6) | Design | | The register reader and the text fixed. F1 is in the working tree, uncommitted and unreviewed (final review). The all-sets gate is a follow-up |
| B12 | LOW | Data flow (lens 5): the width depends on two config tables with no test that their keys agree; Custom Battle reinforcement bearers still get the campaign's infantry-only rule through Patch63 (predates this work); `generate_armory_catalogue.py --check` reports NEW 3 (the hammer); the SandBoxCore warning printed twice per run | Data flow | | Key-set test added; the warning printed once per process. Open: Patch63 in Custom Battle; the catalogue, which waits on two animalia override rows from #646 (still NEW 3 on 2026-09-26) |
| B13 | LOW | Completeness (lens 4): three docs half-updated by the loose-first change; the PatchShield lesson filed where no patch author reads; store test gaps (a key whose `GetHashCode` throws); rollback backups cited beside the live files after the 09:53 sweep had moved them; `set_clip_balance_name.py` applied live at 13:28 with no review and no ledger row | Completeness | | Docs fixed; the lesson moved to `harmony-il.md`; tests added; the docs name the sweep folder (each backup is a row in its `MANIFEST.csv`); the tool has a ledger row and 16 tests, and its review falls to the final review |
| B14 | LOW | Engine (lens 2): the registry's claim about the global slots, two more human-width reads (`StrategicArea.GetAgentFrame`, multiplayer spawning), the teleport flag missing from the log line, a thread test built on `Task.Run(...).Wait()` | Docs, test | | All fixed |
| B15 | LOW | The fix-check lane: a lesson one lane moved cited a test another lane deleted; five lesson counts in the index; one table cell in `armory-guide.md`; the `SubModule.cs` comment for Patch92 still names only the getter postfix | Lanes crossing | Two lanes edited related files without seeing each other's diff | The first three fixed. The `SubModule.cs` comment is a recommendation for the file's owner and still open |

## Incidents outside the reviews

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | MED | `add_hill_troll_hammer_items.py` found its anchor with `^([ \t]*).*anchor.*$` under `re.M` and inserted at the match end. On a CRLF line `.*` takes the `\r` and `$` stops before the `\n`, so the new lines went in between: `weapon_descriptions.xslt`, `crafting_templates.xslt` and `Languages/loc_LOTRAOM_weapons.xml` in the live Armory each gained one `\r\r\n` and one bare `\n`, and the snapshot copy inherited them (git classed it `-text`, a 3,198-line diff for a 2-line change) | Byte handling | The files still parsed, and every gate parses; the tool emitted the file's own terminator on each line it wrote and read that as byte-faithful; no test ran the splice on CRLF text | Repaired 14:13 (`.bak-hillhammer-eol-20260926-141316`); the tool matches a line body as `[^\r\n]*` and ends the match before the line break, with a CRLF test; lesson in `build-tooling-workflow.md`. Found by the docs-pass checker counting terminators |
| C2 | MED | A cook at 07:36 wrote the Armory's `AssetPackages/pack0-9.tpac`. `validate_mesh_refs.py`, `validate_moduledata.py` and `audit_armory_refs.py` preferred a cooked tree wherever one existed, so from then on they read those packs: the hammer, saved in the Kit at 11:39, read MISSING and would have blocked the commit hook, and art deleted from `Assets/` would have passed. The game never read them: the 08:41 session logs `Loading packages $BASE/Modules/LOTRLOME_Armory/Assets` | Stale evidence; repeat | The 2026-09-01 correction proved from the engine log that the loose tree wins, then called the trap unreachable because the Armory had no cooked tree, and the tools kept their order | `tpac_paths_for_modules` reads a module's loose tree first (`test_loose_assets_win_when_both_trees_exist`); lesson in `build-tooling-workflow.md`. The 07:36 packs hold none of the hammer's names, so a player package must come from a cook after 11:39 (lens 7) |
| C3 | HIGH | A scratchpad research script, `re/map/flags-usages/clipparse.py`, opens its first positional argument for writing (`open(sys.argv[1], "w")`, line 106) and reads the roots after it. Run with the live package path first, it truncated `anim_hill_troll_2h_bash_anm.tpac` in the live Armory and wrote JSONL into it. No backup sat beside the package | Subagent write safety | A read-only probe by intent, with a bare positional output; nothing between a scratch script and the game install refuses a write, and no path rule loads for a file outside the repo | Rebuilt from the package's cooked record in `AssetPackages/pack0.tpac` with its segment table emptied, the method first checked on every sibling (`checker/rebuild.py`). On 2026-09-26 the live package (454 bytes, written 14:23:46) is byte-identical to that candidate, and every one of the 480 troll clip packages has an item checksum equal to its RDC stamp (`checker/stamps.py`). The overwritten bytes are kept (`checker/overwritten_bash_jsonl.bak`). No ledger row was written, so the fix-check lane at 14:41 reported the 14:23:46 write as unexplained. Rule proposed below |
| C4 | MED | The tools fix lane refused its brief and changed nothing. The relayed user message that started the workflow was Mike's "What are the next steps for me?"; AGENTS.md says an answer-only request authorizes no edits, and the harness says the relayed request wins over the computed task. The C# lane and both docs lanes read the same input and applied their fixes (the two docs lanes recorded the conflict, the C# lane did not mention it); the checker reported without fixing | Subagent authority | The orchestrator launched an editing workflow in a turn whose user message was a question. Mike's earlier approval does not travel into a lane, and a computed brief carries no user authority | The lane left an exact fix plan and three read-only probes; the tools fixes are in `30abf550`. Rule proposed below |
| C5 | MED | Blender 5.2's `Object.shape_key_add` creates a key at value 1.0, and the FBX exporter writes that value as the channel's `DeformPercent`, so the torn hand file re-imported with all 26 poses applied at once (hands up to 9.27 units off rest) | Tool default | A key's weight is not part of its shape; the tool wrote positions and names and left the value at Blender's default, which is not the artist files' convention (every channel at 0) | Both morph tools set `.value = 0.0` on every key they add; lesson in `animation-skeleton.md`. No reader checks the field yet (B5) |
| C6 | LOW | The commit record does not describe the work. `25995dc9` (pushed) has an 85-character subject with no version label, and its body announces the action trace the next change deleted. `30abf550`, which carries the review fixes, the tools, the docs and the lessons (89 files), is titled "Add unit tests for various tools and functionalities" and its body lists tests only. `/release` builds `CHANGELOG.md` from commit bodies | Process | The subject rule is enforced only by a Claude hook (`check-commit-subject-version.sh`); `core.hooksPath` names `c:\Users\mikew\source\repos\TAOM\.git\hooks`, which does not exist, so a commit made outside Claude is not checked | Report only (an environment setting and Mike's own commits): the player-facing entry (trolls spaced in formation, no troll banners in Custom Battle, both crash fixes, the hammer) belongs in the next commit body or the release note |

## Root-cause patterns

**A gate that cannot see the thing it guards (A1, A12, A13, B1, B4, B5, B6, B7, B8, C1).** Every one of these checks
ran and passed. The hand preview hid the body and the left hand. The seam gate read the seam after the pin had zeroed
it. The morph gate counts channels, not their weights, and a run that checked nothing exited 0. The melee gate keys on
a name prefix and one family of sets. The ladder gate was blinded by an exemption whose reason had gone stale. The
package check tested byte substrings over names that nest by construction. The required clips index was never
compared with the disk. The parse check cannot see a line terminator. Three of these (B4, B6, B8) are the first
review's own fixes, each tested against the failure that prompted it and never against the input that would defeat
it. The 2026-09-25 RCA recorded the same shape ("verification that checks the envelope, not the content"); this time
it recurred inside the fixes.

**A copied template carries state the copy must not keep (the swing CTD, A13, B1, B3).** The generator's blanking line
was written to strip "template strings that belong to the human" on the cave troll path, copied into the
clone-by-name path, and nobody asked what the engine reads the field for: the key was blanked on the assumption
that blanking it did no harm. The hammer kept the mace's head weight, its dead distances and the sentence saying its damage
was proven. The snapshot at `4ceae903` kept 64 crash bindings that a restore would copy back. The ladder exemption
kept a reason that named a weapon the troop no longer carries. In each case the author changed what they came to
change and treated the rest as inert. Two lessons already say this (`data-content-cultures.md`: "A clone that sets
one stat inherits every other stat of its donor" and "Cloning a sibling clones its unverified claims"); they are
lessons, and no rule a tool author loads carries them.

**Stale evidence from a second tree (A4, B13, C2, C3, C6).** Two copies of one thing, and the claim read the wrong
one: the cooked packs instead of the loose tree the game loads; the 08:41 smoke instead of the files as they stood at
09:15; backups cited beside the live files after the sweep had moved them; the snapshot's `action_sets.xml` until it
was re-copied, and its `skins.xml`, stale since 11:28; a live write with no ledger row, which left the next lane unable
to tell a restore from a Kit save; a commit body describing code the next change deleted. `evidence-over-claims.md`
covers trusting one's own earlier result; it says nothing about the moment a run or a copy was taken.

**Subagent authority and write safety (C3, C4).** Two failures of one boundary. Authority: four lanes read the same
input and split three to one. The harness is explicit that the relayed request wins, so the three lanes that
proceeded did so on a computed brief that carries no authority; their edits were in-repo and reversible, and Mike
committed them. Write safety: the brief said never write the game install, and a probe did, through an argument
layout that put its output first. The guard that did work: the morning XML lens's binder dry run was stopped by the
permission check because the script can write the install, and the lens recounted read-only instead.

## Why each review lens caught or missed what it did

| Lens | First review caught | Convergence caught | What it could not see |
|---|---|---|---|
| Standards | The order-preview width gap (A8), the empty catch, the untested store, the model counts, the skill's narrative | The gate row's overclaim, the comment claims, the PatchShield list tied to nothing (B10) | Engine cost and data content: the PatchShield finalizer, the hammer's damage |
| Engine compatibility | The re-deploy onto stale slots (A5), the dropped-banner pickup (A6), the preview read (A8), Patch63's docs; confirmed the native table in the disassembly | The second pickup path, the dismount condition, two more width reads (B14) | It verifies signatures and engine paths; the Armory data and the tools are outside it |
| Efficiency | The PatchShield HIGH (A2), from the attached count in `diag.log`; the only lens that reads the shield's second pass | No defect | Correctness |
| Completeness | No issues (A9), the ten pose-blend statements (A10), #385 (A11), no morph gate (A12), the pushed release note (C6) | The hammer cross-check (B1), half-updated docs, the lesson's routing, the unreviewed `set_clip_balance_name.py` (B13) | It found the morph gate missing; judging what the gate built would count was the tooling lens's job |
| Data flow | The pickup gap (A6), no live crash gate (A13), the deployment footprint (A8), a re-deploy branch never run | The two-table width dependency, the hammer's names, the catalogue drift (B12) | It traces a value to a consumer; a seam offset and a channel weight reach no consumer in code |
| XML and ModuleData | No live crash gate (A13), the 08:41 smoke predating the data (A4), release packaging (A14) | The hammer HIGH (B1), the localization gap (B2), the dead attributes and the seat claim (B3) | Its gates parse, and the CRLF splice parsed (C1, found by the docs-pass checker) |
| Tooling | The wrist seam HIGH (A1), the optional `--clips-index` (A7), the weak round trip | Tooling A: the seam gate (B4), the weights (B5), SKIPPED (B6). Tooling B: the substring check (B7), the index matching nothing (B8), the path spelling (B9) | In the morning, the tools written later that day (the hammer tool, the loose-first change, `set_clip_balance_name.py`); it measured the torn file's seam, not its channel weights |
| Design | Rebuild the slots and delete the re-deploy queue (A5), the pure store with one comparer, the pickup override (A6), the melee gate (A13) | The generator writing the key, the all-sets gate (B11) | |

Most of what the second review found was work done after the first ran (the hammer from 11:40, the loose-first
change, `set_clip_balance_name.py` at 13:28) or the first review's own fixes (B4, B6, B8). Neither review found C1, C3
or C4: a docs-pass checker found C1 by counting terminators, and the fix-check lane saw the effect of C3 without its
cause. The first review's completeness lens found the `25995dc9` half of C6; `30abf550` came after both reviews.

## Feedback to codify

Checked against the rules and lessons first. No new rule for: the SKIPPED exit (B6: "a gate that cannot run has not
passed" is in `moduledata-validation.md` and in a `build-tooling-workflow.md` lesson; the prevention is the exit code
and its pinned tests), the stale cooked tree (C2: fixed in code, lesson filed), the PatchShield tax (A2: lesson in
`harmony-il.md`, which `harmony-patches.md` makes mandatory before any patch), the seam and the weights (B4, B5,
C5: lessons filed; the gates are owed follow-ups and become tier 1 when built), and the git hooks path (C6: an
environment setting, reported under `environment-failures.md`). Where a standing rule is missing, per CLAUDE.md
"Where new knowledge goes":

1. **Prove a tool gate red through its real pipeline** (B4, B6, B8). Tier 3, one line in
   `.claude/rules/moduledata-validation.md` beside "A gate that cannot run has not passed" (it loads on
   `tools/**/*.py`): a new or changed gate is proven by running it end to end on the known-bad input (the torn file,
   the crashing snapshot, a missing install) and reading a non-zero exit; a unit test that hands the check function a
   number is not that proof, and a gate placed after the step that masks its fault reads clean. `hook-authoring.md`
   has this rule for hooks ("Prove a gate live"); tools have none.
2. **A clone names what it keeps** (the swing CTD, B1, B3). Tier 3, in the same rule's paragraph "Also mandatory for
   any script that writes outside the repo": a script that clones a donor block or record lists each field it keeps
   and each it blanks, with why that is still true for the copy (a crafting piece's weight sets its damage; a clip's
   "Blends with animation" box keys its melee row), and prices or verifies the result. Tier 1 beside it:
   `tools/tests/test_melee_ladder.py` fails an exemption whose reason names an item id the troop no longer carries
   (`stale_exemptions` catches only deleted troops today); the generator's `-Verify` key check is in the working tree.
3. **A run proves the files as they stood when it started** (A4, B13, C2). One trap in
   `.claude/rules/evidence-over-claims.md` section C: before citing a smoke, a gate run or a snapshot, compare its
   start time with the last write of every file the claim covers; a run older than the data proves the older data.
   Lesson to append: `docs/reviews/lessons/testing-qa.md`.
4. **Exact tokens for any presence check** (B7). Tier 3, a phrase in `moduledata-validation.md`'s existing
   exact-token sentence, which today covers only deriving a target set: add "and in any check that a name is present
   (a package's table of contents, an id list)".
5. **Splice lines without `.*$`** (C1). Tier 3, one line in the XML I/O convention in `moduledata-validation.md`:
   match a line body with `[^\r\n]*`, never `.*$` under `re.M`, which ends a CRLF line between its `\r` and `\n`.
6. **Launch an editing workflow only on an authorizing turn** (C4). Tier 2, CLAUDE.md "Subagents": a lane takes its
   authority from the relayed user message, never from its brief, so when Mike's latest message is a question,
   answer it and ask before launching edit lanes; an approval from an earlier turn has to be given again. Lesson to
   append: `docs/reviews/lessons/build-tooling-workflow.md`.
7. **A live-install probe cannot write the install** (C3). Tier 2, CLAUDE.md "Subagents" (a scratch script loads no
   path rule, since none fires outside the repo): a probe that reads the game install takes its output by a named
   flag, writes only under the scratchpad and opens the install read-only; a lane that writes the install, a restore
   included, adds a ledger row in the same turn. The ledger half extends "A hand edit to live data is done when its
   replay script, snapshot and ledger say the same" (`build-tooling-workflow.md`, #646) to restores. Lesson to
   append: `build-tooling-workflow.md`.

## Final deep review (2026-09-26 evening)

Nine reports on `30abf550` plus the working tree (the generator key fix and the Brute Force cap, built in the same
workflow): lenses 1 to 7 and one tooling lens. Every lens first corrected its brief: the review-fix lane, the trace and
the tools were no longer uncommitted but in `30abf550`, pushed, so fixes to them go in a new commit. Three fix lanes
(C#, tools, docs) and one checker applied the findings; the checker's verdict was PASS.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| D1 | CRITICAL | The staged `E:/LOTRAOM_Releases/testing` Armory still carries the 2026-09-25 18:08 `action_sets.xml` (32 blocked-family codes on troll clips) and cooked packs from 07:36, before any clip was keyed. A player on that build crashes on the first blocked troll attack (lens 7, confirmed by the orchestrator: sha256 `f705b34f...`, 16 `act_blocked_*` and 16 `act_quick_blocked_*` troll bindings) | Stale evidence from a second tree; repeat of C2 | Every gate reads the live install; the release folder is a third copy nobody checks, and no tool reads keys inside cooked packs | Reported to Mike: do not publish it; re-cook the Armory in the Kit after the keys, take the 14:13 `action_sets.xml` and the 14:07 crafting pieces, smoke the packaged Armory. Publication status UNVERIFIED |
| D2 | MED | `keyed_clips` read only the package, so a re-cut (which writes packages with no RuntimeDataCache entry) would bind the 32 melee codes to packages the engine skips, with the wire gate green; a test pinned the gap (lens 5) | A gate that cannot see; repeat | The gate was written from the key field; the RDC skip is a separate trap ("Unsaved tpac") nobody joined to it | `keyed_clips` requires the item's RDC entry with its stamp equal to the checksum; the binder refuses a module with no `RuntimeDataCache`; tests for a missing, stale and foreign entry |
| D3 | MED | Two definitions of self-keyed: `keyed_clips` ignored Blends with action, `check()` and the generator's `-Verify` required it empty (lens 6 P1) | Drift between gates | Each gate was written in its own lane | One `is_self_keyed`, used by `keyed_clips`, `check()` and `apply()`'s read-back; a test with a leftover Blends with action |
| D4 | MED | `wire_hill_troll_race.py --check` reported a +0x6590B9 swing crash when its clips folder did not exist, having read nothing (tooling T1); its test passed only because it never passed the folder | A gate that cannot see; repeat of B6 | The new check reused the gate's exit path without a "could not run" branch | UNVERIFIED, exit 2, when the clips folder or `RuntimeDataCache` is missing; three tests for missing, unkeyed and saved |
| D5 | MED | A top-level `import xxhash` in `set_clip_balance_name.py` broke the binder, the wire gate and three test modules on any Python without it (tooling T2) | Dependency | The tools had been stdlib only; the import came with the shared helper | Lazy import in the two hashing paths; the test module skips cleanly; a discovery run with xxhash blocked passes |
| D6 | MED | The write path that changed 28 live packages and 28 RDC entries had no test: refusals, backups, restore, `--check`'s exit (tooling T3, lens 4 L5) | Test coverage | Its first review was this one; the unit tests covered parsing and the byte edit | `MainTests` on a temp tree; the plan now dedupes names and computes every RDC entry before the first write (T4) |
| D7 | MED, refuted | Lenses 2 and 5: 24 of the 30 self-keyed clips are at Loading Type 2, whose loader passes a null keyframe set, so the swings might not animate | Engine reading | | Refuted as stated: 254 of vanilla's 347 unkeyed Loading Type 2 clips are bound directly by 320 live nodes (98 conversation gestures in `as_human_warrior`, the town cats' and dogs' walks and deaths, a tavern sit-and-drink idle), so 2 does not stop a directly bound clip from playing. Through a self-keyed row it stays UNVERIFIED, low risk; a look at a troll's quick overswing is owed. The orchestrator's first census counted commented-out bindings (rider falls, the drinker set); the docs lane re-ran it with a parser. That repeats `xslt-moduledata.md` "XML comment spans are invisible to line-oriented greps", which loads for no script outside the repo |
| D8 | LOW | The `[TrollClips]` line and its tests said "which clip that action played"; `MBActionSet.GetActionAnimationName` takes no agent, so the line names the set's binding (lenses 1, 2, 5). The community guide repeated the overclaim | Claim beyond evidence | The trace was written to answer "which clip", and its wording answered "did it play" | Comments, tests and the tag ("melee-table family") corrected; the guide's third pass pins "bound in battle with no crash, motion not yet confirmed" |
| D9 | LOW | Six docs still said the generator blanks the key (lenses 1, 2, 4, 5) | Claim not swept; repeat | The generator fix was built in the same workflow as the review | Each place corrected; a lesson note appended |
| D10 | LOW | Tooling: `parse()` read key fields on metadata versions that lack them (T5); `index_clips` crashed on a truncated package; a morph-gate test passed with the gate reading nothing (T6); README rows split by unescaped pipes; the hammer tool did not re-check a restored head weight (lens 4 L6) | Tooling | | All fixed with tests; README row 252 (`add_collision_body.py`) has the same pipe defect and predates this work |
| D11 | LOW | The system map: a wrong class name, a wrong "correction" of a cite, section 4 cited from a scratch decompile, two incomplete engine descriptions, stale "uncommitted" notes (lens 2) | Docs | The map was written from several decompile copies | Each checked against the taom-src cache or the DLL and corrected |
| D12 | LOW | Four guard tests added after their fixes were never seen failing (lens 1) | TDD | | Each mutated, seen failing, reverted byte for byte (hashes checked) |

**Design proposals applied (lens 6):** the cap drawn with `MBRandom.RandomInt(RingMinTargets, RingMaxTargets + 1)`
(v1.5.3 `MBFastRandom.Next` excludes the upper bound), deleting `RingTargetCap`, its NaN gate and 18 test cases; the
nearest-N pick in the `PlanAmbush` shape; the generator's unreachable keyed-name refusal removed and a TOO LONG check
added to the Fab path; the Armory-only pack scan. `_WARNED_EMPTY_MODULES` stays: two passes still list the Armory in
one run. **Declined:** sharing the trace's troll scan (lens 3 F1), since the trace is deleted after the tuning pass.
**Deferred:** renaming `set_clip_balance_name.py` (lens 6 P6), since closed issues #683 and #385 and the guide cite
the path; the trace's deletion (lens 6 P7), after tuning.

**Verification after the fixes:** `dotnet test TAOM.Tests` 10,838 passed, 0 failed, 2 skipped (10,856 before, less the
18 deleted cases); the touched tools tests 404 passed; the full `tools/tests` 2,522 passed with one failure that
predates this work (`test_generate_career_kits`); `wire_hill_troll_race.py --check` on live OK with 30 self-keyed;
`set_clip_balance_name.py --check` 30 OK; the binder dry run "no change"; the three generator `-Verify` runs clean;
`validate_moduledata.py` 0 errors; `lint_docs.py --fail-on-drift` exit 0.

**Patterns.** Two of the four themes above recurred once more. A gate that cannot see (D2, D4) came back in gates
written the same day, the same shape as B4, B6 and B8; feedback item 1 covers it. Stale evidence from a second tree
(D1) reached a copy outside the repo and the install: the release folder. Feedback item 3 covers the reasoning, not
the folder, so one more item:

8. **A release folder is a copy the gates never read** (D1). Tier 4, `/release` and
   [release-process.md](../reference/release-process.md): before a build is published, compare the staged Armory's
   `action_sets.xml` with the live one and run the melee gate against the staged module, and cook the packs after the
   last live Armory write the release depends on.

**Still owed after the final review:** in game, the cap smoke (every `ring=` line with Hit at most Cap at most 5), a
full `act_release_*` and `act_blocked_*` swing, a look at the Loading Type 2 quick swings, and the deployment teleport
replay; Mike's decision on the testing release (D1); README row 252; the `SubModule.cs` Patch92 comment and the
`AgentAdapterCache` comparer (carried over); the hammer tool's `(?=\r?\n|\Z)` anchor.
