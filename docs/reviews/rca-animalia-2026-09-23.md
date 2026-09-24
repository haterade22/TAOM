# RCA: Animalia elk and moose, riders and Monster size (deep review, 2026-09-23)

## Top-line

`/deep-review` of #646 as it stood on the evening of 2026-09-23: the Animalia elk and moose (Fab packs reskinned onto
`horse_skeleton` with their own clips), their antler attack on the shared elephant-like engine, the great elk's resize
to 110, the tooling and data that Mike's unlabelled "commit all" (`c79a5852`, 12:47) had swept in unreviewed, and the
live `LOTRLOME_Armory` edits. Eight lenses in two waves of four, all on the `deep-reviewer` definition (Opus 5.5, max
effort): Standards, Engine compatibility, Data flow, XML; then Efficiency, Completeness, Design, Tooling.

**No CRITICAL or HIGH, no engine incompatibility.** Engine verified 21 API uses against the installed v1.5.3 DLLs and
found 4 wrong claims in text; XML ran 11 gates, both failures older than this change.

Two instructions from Mike arrived during the review and are part of what it covers: new riders (the moose to
Thranduil and the lords, the great elk to the top cavalry only, the Animalia elk to the lower cavalry and, by his
answer, the `elk_rider` career start) and **"The monster xml should control the size of the animal"**, which became
`Main/Features/MonsterSize/` ([monster-size.md](../features/monster-size.md)).

Findings, merged across lenses: **12 MEDIUM** (rows 1 to 12) and the LOW findings grouped into rows 13 to 22. Four
behaviour-changing proposals went to Mike in one question batch.

## Mike's decisions (asked once)

| # | Question | Answer | Applied as |
|---|---|---|---|
| D1 | The two `taom_test_*` riders show in every Custom Battle's Mirkwood cavalry picker | "Keep both visible" | Recorded as a release blocker in the feature doc, the workflow doc and the CHANGELOG |
| D2 | Script fixes | Guard against a running game or Kit; the Armory writer's dry run and tests; the clip generator's `-Verify` (not the census's empty-run refusal) | Applied by a tooling builder (verification below) |
| D3 | The two item names | Register English only | Rows added to the Armory's `loc_LOTRAOM_horses.xml`; translation owed |
| D4 | Stale issues | Post comments on #646, #636, #638 | Posted after the final verification |
| D5 | Where a mount's size lives | "On the Monster" (`taom_body_length`) over a TAOM table or keeping the item's `body_length` | `MonsterSize` feature; the items keep the schema-required placeholder `body_length="100"`; reach follows `Agent.AgentScale` |

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `apply_animalia_armory.py` wrote the moose at `body_length` 100 while the live item was 150: the reinstall recipe silently reverted Mike's resize, with a 1.5x reach left on a 1.0x moose | replay-script drift | The resize was a hand edit through a one-off; nothing ties the replay script's recipe to live state, and the recipe's step 5 ran no size test | The recipe now writes the size on the Monster; the ledger's step 5 runs `FullyQualifiedName~Animalia`; lesson "a live hand edit is done when its replay script says the same" |
| 2 | MED | The in-repo Armory snapshot lacked the +80 / +5 Animalia lines, so its README restore path would drop both sets while the Monster file still names them | companion artifact skipped | The Animalia ledger never mentioned the snapshot; the workflow had no snapshot step | Snapshot refreshed; README rows updated; lesson (same as 1) |
| 3 | MED | The test riders appear in the Custom Battle picker for every player | test data in shipped UI | "CustomGame only" read as "hidden"; the picker lists every Soldier of a culture (`ArmyCompositionGroupVM`) | Mike: keep, delete before release (D1); lesson: CustomGame-only is not hidden, `is_obsolete` is |
| 4 | MED | `AnimaliaConfig`'s Monster and set ids were pinned only against themselves; a typo would pass the suite and never attach a tree | tautological pin | The wiring tests used their own literals; the service test fed the constant back into a service built from it | Literal pins; the wiring rows built from the constants; lesson in testing |
| 5 | MED | "An older campaign keeps Thranduil on the great elk" (5 places): every released build's saves have the `charger` | state described from the working tree | The great elk's lord wiring was never in a release; the claim was written from the tree, not from `v2.0.30` | Reworded; lesson: describe a save's contents from the last released data (`git show <tag>:...`) |
| 6 | MED | The owed in-game list missed the campaign surfaces the new riders opened (the Blunt kill, the career start, the `_map` sets, an existing save) | incomplete owed list | Riders were added after the owed list was written | Checklist rewritten in the feature doc |
| 7 | MED | The three live-Armory writers ran with the game or Kit open (a Kit save undoes the skeleton patch) | missing house guard | New tools did not copy the newest sibling's guard (`skeleton_hit_capsules.py`) | Guard added (D2); lesson: every live writer refuses while the game or Kit runs |
| 8 | MED | The Armory writer's antler step sat outside its dry-run envelope: the documented dry run crashed on a fresh Armory, and a failing step wrote steps 1 to 4 first | step added outside the envelope | Step 5 was appended after the envelope existed; the script had no test | Pure antler edits inside the envelope plus a synthetic-tree test (D2); lesson |
| 9 | MED | The clip generator skipped existing clips unchecked; a Kit re-import leaves orphaned or stale clips reported as `errors=0` | missing verify mode | The troll generator's `-Verify` was not ported | `-Verify` (D2) |
| 10 | MED | The census can pass having checked nothing and patches before judging | silent clean | Counted packages against animations | NOT APPLIED: Mike declined (D2) |
| 11 | MED | `apply_animalia_armory.py` had no tests | untested writer | Siblings have them; this one was written in a hurry | Test file (D2) |
| 12 | MED | Stale status lines claiming "not deployed" while a 13:42 deploying build (not this session's) already carried the attack | status from memory | The build was another session's | Docs updated; the deploy fact recorded |
| 13 | LOW | `_town_and_village` described as engine-required or engine-derived in the workflow doc, the ledger, the apply script's comment and the live `action_sets.xml` comment | **repeat**: engine claim restated | The correction was recorded the same day (`lessons/data-content-cultures.md`, "Cloning a sibling clones its unverified claims") and in `creature-mount-authoring.md`, but the new text was written from memory of the sibling | All four corrected; `/new-creature-mount` Phase 4 now states the fact where creature work starts |
| 14 | LOW | "The horse rig has no attack animation" (it has `act_horse_kick`) | copied engine claim | Copied from the war ram's headline | Corrected in the Animalia doc |
| 15 | LOW | The test comment gave the wrong reason for `actt_kick` (IsAttack compares indices) | wrong rationale | Reason written from assumption | Corrected with the verified reason |
| 16 | LOW | "`[MountSpawn]`" promised for console spawns (only the preview spawner logs it) | wrong log recipe | Not checked against the patch | Corrected |
| 17 | LOW | The great elk's "auto-resolve" claim: `Effectiveness` feeds only the tournament simulator | wrong engine claim | Older text, never traced to a caller | Corrected with the caller (`TournamentFightMissionController.cs:499`) |
| 18 | LOW | "A quarter to three quarters" from figures giving 0.20 to 0.58 | arithmetic | Not recomputed | Corrected |
| 19 | LOW | Clip-length constants with no reader, asserted against their own literals | tautology | Written as documentation | Deleted; the cooldown test compares with the clip frames |
| 20 | LOW | Four public types in one file; test names not `Method_State_Expected`; a stale "2x" in `ElkConfig`'s summary and three docs; "only the elk" in six comments; the #629 rationale overstated; the saddle's #646 dependents unrecorded; stale INDEX, feature-map, handbook and marketplace lines; the provenance row missing `Main/Features/Animalia/**`; the item names unregistered | convention and drift | Each written before the next change moved it | All fixed |
| 21 | LOW | `IoC.Resolve` in the static `AnimaliaCombat` (the same pattern as four siblings) | service locator | A sibling pattern | NOT APPLIED: a policy call (name the profile holders as boundary code in `.ai/review-reference.md`, or refactor all five); left to Mike |
| 22 | LOW | `anim_animalia_elk_stand_eating_03` generated but never bound | dead data | Five idles for four slots | NOT APPLIED: no runtime effect |

## Found while fixing (not a review finding)

- **`ItemObject.Effectiveness` is cached at load** (`ItemObject.cs:112`, set at `:476`/`:671`). Moving the size off
  the items would have left the three mounts' tournament rating computed without it. The adapter recomputes it after
  writing `BodyLength`.
- **`body_length` is required on `<Horse>`** (`Items.xsd`): the first cut removed it from the three items, and the live
  schema gate failed them. The items keep the placeholder 100, pinned by the data tests.
- **A module `Monsters.xsd` would crash the engine's merge**, not quiet the validation line:
  `MBObjectManager.MergeElements` indexes `XmlResource.XsdElementDictionary`, filled only from the game's schema
  folder. Found before shipping one; the accepted cost is one red "not declared" line per sized Monster.

## Root-cause patterns

1. **Text asserted from memory.** Findings 5, 13 to 18: each claim was written in the same session without reading
   the engine, the release, or the repo's own correction. The evidence rule already covers it; what failed is
   reading the relevant lesson file before writing, and #13 repeated a lesson recorded that morning. The fix moved the
   repeated fact into the skill that starts creature work.
2. **A live edit without its companions.** Findings 1, 2 and 12: a hand edit to live data, and a build nobody here
   ran, left the replay script, the snapshot and the status lines behind.
3. **New writers without the house guards.** Findings 7 to 11: game/Kit guard, dry-run envelope, tests and a verify
   mode all existed in siblings and were not copied.

## Why each lens missed nothing it should have caught

Every lens reported inside its remit. The repeat (13) was caught by three lenses at once (Engine, Data flow, XML). The
two items found while fixing were outside the review (they came from the Monster-size design).

## Verification

**The script fixes (D2)** were written by a builder agent and re-run by the orchestrator: the three tooling test
files pass, the writer's dry run exits 0, the clip generator's `-Verify` reads 54 clips ok, the master census 97 ok.
The builder ran one path-limited `git stash` and `pop` against the no-git rule; `git stash list` afterwards held only
an older, unrelated stash and every marker of this change was intact.

**The first cut of the size move removed `body_length` from the three items**, and `validate_xml_schemas.py --live`
failed them: `Items.xsd` makes it required. The items keep the placeholder 100 (see "Found while fixing").

**Convergence pass** (4 lenses on the fix diff, defects only): Standards 1 MEDIUM and 8 LOW, Engine 5 LOW, XML 4 LOW,
Tooling 7 LOW. Fixed: the MEDIUM (a Monster size could move the elephant or mumakil under its baked howdah or
tower; `HowdahPrefabTests` and `MumakilPlatformTests` now fail if either Monster declares one), the reach floor (the
engine's single-precision `0.01f * 10` is 0.099999994f, under the old 0.1f; the test walks all 991 sizes), the size
re-read now uses the engine's game-type filter, a summary warning on refused sizes, the failure path counting what
was written, one shared parser for the service and the data tests, wiring pins for the IoC line and the call site,
the test names, the writer's false "already declared" lines, `-Apply` re-checking every run, the shared guard's
tests moved where CI runs them, a bad `--game-dir` exiting 2, and the stale doc lines (the expected summary line,
the camera claim, the attribute count, the ledgers, the resize procedure's replay step). Not fixed: the war ram's
and spider's older `_town_and_village` comments in the live Armory (another feature's, one recording a crash),
noted on #646 instead.

**Final runs (this tree):** `dotnet test TAOM.Tests` 10,287 passed, 0 failed; `pytest tools/tests` 2,044 passed;
`validate_moduledata.py` 0 errors (1,591 warnings, unchanged); `validate_xml_schemas.py --live` 241 files pass;
`audit_action_set_parity.py` exit 0; `lint_docs.py` 0 dead links, 0 long dashes; `test_hooks.sh` 283 passed.

**Not yet run:** anything in game (the deploy is held while another session has uncommitted edits in the tree).

## Feedback memories to codify

None new: the rules exist (evidence-over-claims §C; read the lesson category first). The lessons appended to
`lessons/build-tooling-workflow.md`, `lessons/data-content-cultures.md`, `lessons/testing-qa.md` and
`lessons/state-lifecycle-save.md` make the three patterns searchable by subsystem. The convergence pass's MEDIUM (a
Monster size would move the elephant or mumakil under its baked platform) is pinned by `HowdahPrefabTests` and
`MumakilPlatformTests` and documented in `monster-size.md`; it was caught before it shipped, so it has no lesson.

## Final review (the whole session, 2026-09-23 night)

Mike asked for "a deep review and codex review of the entire sessions work": everything above plus the marketplace
routing, the live Armory edits, the tools, the harness switch to Opus 5.5 and the docs. Eight `deep-reviewer` lenses in
two waves of four (Standards, Engine, Data flow, XML; then Efficiency, Completeness, Design, Tooling), all on Opus 5.5
at max effort, one Codex adversarial pass (`gpt-6-astra`, `ultra`, prompt
`docs/reviews/codex-adversarial-animalia-2026-09-23.prompt.md`, output `docs/reviews/raw/codex-adversarial-animalia-2026-09-23.md`),
then one convergence pass on the fixes. Baseline 10,303 C# and 2,044 Python tests passing.

**No CRITICAL, no engine incompatibility.** The Engine lens and Codex both settled the load-bearing engine claims from
the installed v1.5.3 DLLs: an undeclared `taom_body_length` loads with the element intact and one red validation line;
items are loaded and final at `OnGameInitializationFinished` in every game type and nothing reloads them; the only
`BodyLength` readers are the build scale, the camera and `CalculateEffectiveness`; the three reflection targets exist;
`actt_kick` sits outside Rear and the half-open 48 to 51 struck band. Two HIGH, five MEDIUM, and LOWs.

### Mike's decisions (asked once)

| # | Question | Answer | Applied as |
|---|---|---|---|
| D6 | The moose can be drawn into Mirkwood markets; the docs said it is not sold | "Sold: let it appear" | No blacklist; seven texts corrected |
| D7 | Replace the tuned 0.099 reach floor with a positive gate (Design proposal 1) | "Apply it" | `ElephantLikeReach` gate `(0, 10]`, tests first |

### Findings

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| F1 | HIGH | The first fix round removed `wire_anim_master_skeletons.ps1`'s `-BoneCount`, its only rig check: any EMPTY master could be re-pointed at `horse_skeleton`, and the live warg folder holds one (50 bones) | Tooling | regression from a fix | The convergence pass ran the tool on the elk folder only, one rig | Rig check restored (BoneNum per rig, WRONG RIG, exit 2 for an unknown rig), proven on the warg, elk and troll folders; lesson; the tooling lens asks what a removed check rejected |
| F2 | HIGH (XML) / MED (Data flow) / P2 (Codex) | "The moose is not sold" in seven texts, and in the review scope as Mike's decision: TAOM's culture pool ignores `is_merchandise` and draws the moose into Mirkwood markets | XML, Data flow, Codex | wrong claim about data | The claim was inferred from a vanilla flag, never traced through TAOM's market; the session's wording was promoted to a decision | D6; the texts corrected; lesson (trace availability through TAOM's systems; quote Mike) |
| F3 | MED | MonsterSize's three by-name reflection sites were not in `ReflectionSiteBindingTests` / `reflection-sites.md`, and a missing recompute target logged nothing | Engine, Codex (O1) | missing gate | Designed inside the first fix round; no lens checks the catalogue | Three rows; the adapter logs a missing or failing recompute; the Engine lens now reports a missing row |
| F4 | MED | The clip generator read a failed or partial measurement as zeros (PowerShell `$null` to `[float]` 0), and the measure run's `.DONE` said ok | Tooling | unvalidated input | The committed data are clean, so the path never ran | `Get-MeasureProblem` (BAD MEASURE), `.DONE` fails on any failed clip; proven with a broken report; lesson |
| F5 | MED | The live Armory ran ahead of the code: the installed DLL has no size pass and the old reach, so any smoke before a deploy reads 1.0x bodies with 1.5x reach; HEAD's committed tests fail against the live install | Data flow, Completeness | sequencing | The unversioned-module trap covers a reinstall reverting a live edit, not a live edit outrunning its code | Feature and size docs: smokes only after a deploy; code, tests and ledgers commit together; lesson |
| F6 | MED | The handbook's "Resize a mount" recipe still sent the reader to the item's `body_length` | Completeness | stale how-to | The doc sweep followed the feature's links; the recipe does not link to it | Step 0, the attribute row qualified, a doc-lookup row; lesson |
| F7 | MED | The test-rider release blocker (D1) was in three docs and not on the release path | Completeness | tracking in prose | Writing it down felt like tracking it | `/release` pre-flight step 5 (`git grep -l taom_test_ -- Main/_Module`); lesson |
| F8 | LOW | The template still said the horse rig has no attack clip, and the skill that a reskin has no clips: RCA row 14 fixed a copy, not the source | Completeness, Standards | **repeat**: copied claim | Row 14's fix was applied where the finding pointed | Fixed in the skill, the authoring doc and five copies; recurrence note on the "Cloning a sibling" lesson |
| F9 | LOW | Sixteen smaller defects: the skill's strike-band line and description; a dead size dropped silently; a literal attribute name in two prefab tests; a test name; two history tails; the workflow doc's log file and per-pack list; the size gate's recipe not tied to live; the Advanced Start route; no Animalia wiring or reach-flag pins; the removal note in the rider file; clip names not verified; stale Kit steps and backups; the shared guard crashing on a non-ASCII process name; an empty `--game-dir` falling back to the live install; checksums unverified; host errors on exit 1; no byte restore on a failed read-back; one-id presence checks; the creature doc's reach derivation; the lesson total; feature-doc gaps; the ledger's outside-repo tools and section order; the provenance row; a lesson that promised tests the PowerShell tools cannot have | all lenses | convention and drift | Each written before the next change moved it | All fixed (verification below) |
| C1 | MED (convergence) | The CHANGELOG still said "the moose is not sold" | Convergence | missed copy | The fix list named the CHANGELOG for last, as its own pass | Fixed with the CHANGELOG pass |
| C2 | LOW (convergence) | The fix round's new comment said `is_merchandise` keeps the animals out of caravans; `CaravansCampaignBehavior.BuyCategory` never reads it (v1.5.3) | Convergence | wrong claim introduced by a fix | The Data flow and XML lenses' list of `NotMerchandise` readers was taken on trust | Four texts corrected, the live comment with backup `.bak-caravan-20260923` |
| C3 | LOW (convergence) | The skill's reskin paragraph dropped the `_map` duty; the workflow doc's per-pack list missed the loops and prefixes; WRONG RIG claimed more than a bone count proves (chariot and elephant share 60); lesson sources named a section not yet written; smaller nits | Convergence | drift | - | Fixed |

**Not applied, with the reason:** `MonsterSizeService.cs:31` spelling out `System.Globalization.CultureInfo` (Standards
nit): `TAOM.Adapters` also defines a `CultureInfo`, so the short name is ambiguous there. Design proposal 3 (the troll
clip tool reusing the batch patcher) and Efficiency's `Selector.Prepare` allocations and `DateTime.Now` in the BT
timers: pre-existing shared code, follow-ups, no issue filed (filing is public; Mike's word). Vendoring
`TolerantTpacLoader.cs` into `tools/` and failing the reskin and retarget `.DONE` on partial results: follow-ups. RCA
row 21 stays Mike's call.

### Root-cause patterns

1. **A fix judged on the input it was written for.** F1 and C2: the first fix round removed a check without asking what
   it rejected, and this round asserted a list of engine readers it had not checked itself. The remedy is the same
   one the evidence rule gives for claims: run the fixed tool on the case the removed check existed for, and open the
   engine code behind every "the engine does not" sentence.
2. **A claim about data taken from a flag or a session's own words.** F2 and F5: "not sold" from `is_merchandise`, and
   "the installed game shows the new sizes" from the repo. Both are statements about what a player sees, and both
   needed a trace through TAOM's systems or the install.
3. **Knowledge left where it was written, not where it is used.** F6, F7, F8: the handbook recipe, the release path
   and the skill are where a later reader acts, and each kept the old text.

### Codex and the Claude lenses

Codex's one P2 (the moose market) matched the XML and Data flow lenses independently, and its P3 (the recompute not
atomic) matched the Engine lens's gate finding from another side. It verified all seven known suspects with decompiled
code and ran the PowerShell tools read-only under a patched host. It missed the tooling regression (F1) and the
completeness items; the lenses missed nothing Codex found. One disagreement: Codex described the struck band as 48
through 52; the Engine and Standards lenses read `MBMath.IsBetween` as half-open (48 to 51). No conclusion changes.

### Verification

Fix round: TDD for the C# (four new tests RED, then GREEN) and the writer (six RED, then GREEN). After the fixes, before
the convergence pass: `dotnet test` 10,313 passed, 0 failed; `pytest tools/tests` 2,054 passed; `test_hooks.sh` 283;
`validate_moduledata.py` 0 errors / 1,591 warnings; `validate_xml_schemas.py --live` 241 pass;
`audit_action_set_parity.py` exit 0; `lint_docs.py` 0 dead links, no dashes in this change. Tools, read-only on the live
tree: the patcher census ok=97 (elk), ok=52 (troll, human rig), WRONG_RIG=1 (warg); the generator `-Verify` ok=54, its
dry run errors=0, BAD MEASURE on a broken report; on a scratch copy a renamed clip and a flipped byte fail `-Verify`;
the writer's live dry run says "nothing to write" and its recipe matches the live Monsters and items attribute for
attribute. The final numbers after the convergence fixes are in the CHANGELOG entry and REVIEW-LOG 132.

Not yet run: anything in game; it waits for a deploy (F5).
