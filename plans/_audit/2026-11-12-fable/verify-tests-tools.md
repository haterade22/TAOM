# verify-tests-tools: refuter for lanes tests and tools

Baseline `7087fa633`, worktree `wt-baseline` (git status empty before and after every run below).
Read-only. I ran no dotnet. Every Python script I ran is in
`E:\repos\taom-improve\2026-11-12\scratch\verify-tests-tools\` and only reads. Timings are from this
desktop, warm file cache, while other agents were running; they are floors or ratios, not player numbers.

Verdict scale: CONFIRMED (facts hold, change is coverage-preserving as proposed), NARROWED (part holds,
or holds only under conditions the lane did not name), REFUTED (the load-bearing claim fails).

---

## TST-01: cache the Main/Client C# corpus in RepoPaths. NARROWED

**Re-read:** `TAOM.Tests/Infrastructure/RepoPaths.cs` in full (88 lines): `CSharpFiles` :27-33,
`ReadSource` :49-58, `MentionsOutsideComments` :71-80. `RepoPathsTests.cs` in full (128 lines).

**Ran:**
- `callsites.py`: 67 `CSharpFiles(` call sites (the lane said 56 in 50 files). Arguments:
  `()` 42, `("Main","Client")` 7, `("Main")` 7, `("Main","Client","Dependencies")` 4, `("Client")` 3,
  `("Main","Dependencies")` 1, `(ScannedRoots)` 1, `(CodeRoots)` 1, `(CodeRoots + "CoopCompat")` 1.
  59 of the 67 read file text within 12 lines of the call; 7 of those are static or `Lazy` helpers
  (read once per class already); about 8 filter the path list to one feature folder before reading
  (`MarshalMarkCaptureAndShotTableTests.cs:552`, `TaomMarshalHotKeyCategoryTests.cs:144`,
  `TaomSeatHotKeyCategoryTests.cs:143`, `WardensTallyWiringTests.cs:107`, `LongDefeatSallyWiringTests.cs:59`,
  `LongDefeatAdapterRulesTests.cs:107`). So whole-tree text passes per run are about 45 to 50, close to
  the lane's 56 but not equal.
- `writes.py`: every TAOM.Tests file that both writes files and resolves repo paths. 23 files; 22 write
  only under a temp folder. The one exception is `RepoPathsTests.cs:49-58` and `:107-117`: it writes
  probe files under `TAOM.Tests/bin/`, and `MentionsOutsideComments_ReadsCodeNotComments` (6 DataRows,
  `:98-118`) rewrites **the same path** `TAOM.Tests/bin/repopaths-mentions-probe.txt` with different
  content per row. `:120-127` expects `FileNotFoundException`, `:70-75` expects `AssertFailedException`
  from `ReadSource` on a missing file. No test writes under `Main/`, `Client/`, `Dependencies/` or
  `CoopCompat/` (scope: the 2,403 tracked `.cs` files under TAOM.Tests).
- `readtime.py` (Python floor of one `CSharpFiles()` pass, enumeration included): 4,610 files,
  17,607,092 characters, 0.29 to 0.32 s per pass (enumerate 0.04 s, read and decode 0.25 s).
- Test host: one test assembly (`TAOM.Tests.csproj`, net472, MSTest 3.1.1), no `[assembly: Parallelize]`,
  no `DisableAppDomain`; `binding-gate.runsettings` sets only TreatNoTestsAsError, an env var and
  MapInconclusiveToFailed. A static cache lives for the whole run.

**Corrections to the lane:** 67 call sites, not 56; two callers read with `File.ReadAllLines`
(`LocalizationKeyConsistencyTests.cs:265`, `UnregisteredLocalizationKeyBaselineTests.cs:113`), which a
text cache does not replace as is; `CommandTrialWiringTests.cs:124` reads each file twice. Several
callers sit in skip-list folders (`CoopInterop`, `LongDefeat`, `FordsOfWar`): their call sites must
not be edited, though they still benefit from a cache inside `RepoPaths`.

**Conditions for a coverage-preserving change:**
1. The cache holds raw decoded text only for files under the four code roots (`Main`, `Client`,
   `Dependencies`, `CoopCompat`), obj and bin excluded. Every other path (`TAOM.Tests/bin` probes, any
   `ReadSource("TAOM.Tests/...")`) reads the disk every call. A cache keyed on every path breaks five of
   the six `MentionsOutsideComments_ReadsCodeNotComments` rows and the two missing-file tests.
2. A missing file keeps its exception: `ReadSource` must still `Assert.Fail`, `MentionsOutsideComments`
   must still throw `FileNotFoundException`.
3. Root sets differ per caller, so key the cache per root (one list per root, concatenated in the
   caller's root order) and keep the file order of `Directory.EnumerateFiles` (a `List`, not
   `Dictionary` key order).
4. Leave the two `ReadAllLines` callers and the skip-list call sites unchanged.
5. Proof: the full suite total equal to the base, plus the existing `RepoPathsTests`.

**Measurement the plan's first step must take:** inside one temporary test, time three consecutive
`RepoPaths.CSharpFiles().Select(File.ReadAllText).Count()` passes with a Stopwatch (the first is cold),
and count the whole-tree passes in one run (a static counter in `CSharpFiles`, removed before commit).
**Threshold:** go on only if (passes minus 1) times the warm per-pass time is at least 5 s (about 4% of
the 112 s suite). The Python floor gives about 0.3 s times 45 = 13 s, so the change is probably worth
it, but the regex and comment-strip work in each gate is not saved by the cache.

**Recalibrated:** impact up to about 13 s per suite run (floor-based, UNVERIFIED in .NET); effort S;
risk LOW with the five conditions, MED without condition 1.

---

## TST-02: build the IL call index once. NARROWED (measure first)

**Re-read:** `Migration/IlCallScanner.cs:28-58` and `:110-140`, `Infrastructure/TaomAssemblies.cs:15-44`.
`git grep` of `TaomAssemblies.FindCallers|IlCallScanner.FindCallers`: 9 TaomAssemblies calls in 8 files
(each walks 2 assemblies), 3 direct walks of a TAOM assembly, 7 walks of engine assemblies. The lane's
list is correct.

**Corrections:** `FindCallers` stops reading a body at the first banned call (`ExtractCalledMethods(...)
.Any(isBanned)`, `:55`, a lazy iterator), so an index that records every callee does more work on its
first build than one current walk. The runtime also caches resolved method handles, so the second and
later walks are likely cheaper than the first. The lane's saving assumes 13 equal full walks; that is
UNVERIFIED.

**Conditions:** the index must keep `scanned` and `unreadable` per assembly with today's meaning, the
"unknown opcode: stop this body" rule (`:122-123`) and the skip of unresolvable tokens; cache only the two
TAOM assemblies.

**Gate before a plan:** time `TaomAssemblies.FindCallers(m => false, out _, out _)` twice in one test
process. If the second call costs under 0.5 s, the 13 walks cost under about 7 s and the change is not
worth an index type; drop it. Without that number this is not plan-ready.

**Recalibrated:** impact UNVERIFIED (0 to several seconds); effort M; risk LOW to MED.

---

## TST-03: skip `Languages/` in three ModuleData scans. NARROWED (true, small)

**Re-read:** `Features/EyeOfSauron/HuntTroopListsDataTests.cs:22-43`, `Features/FireAndTribute/FireAndTributeDataTests.cs:57-80`,
`Features/NamedCompanions/NamedCompanionShippedDataTests.cs:70-89`.

**Ran:** `git grep -l '<NPCCharacter' -- Main/_Module/ModuleData/Languages` prints 0 files; same for
`<Item[ >]` and `<Item\s+id=` (0). No untracked or ignored files under ModuleData
(`git ls-files -o`, `-o -i --exclude-standard`). Tracked XML: 289 under `Languages/`, 168 elsewhere.
`xmltime.py` (Python ElementTree floor): Languages 31,374,231 bytes parse in 0.53 s; the rest,
20,514,994 bytes, in 0.37 s.

**Intent:** both character loaders and the class comment of `HuntTroopListsDataTests` (":14-15", "Every host
troop must exist as an NPCCharacter in Main/_Module/ModuleData") look for characters. A Languages file is
a string table; the folder holds none today. Excluding it keeps the contract "find the NPCCharacter
definitions", not "parse every XML". Both loaders sit in a static `Lazy`, so each class parses the tree
once; the saving is about two Languages parses per run (0.5 s floor each in Python, XDocument is slower:
about 1 to 3 s in all, UNVERIFIED). The NamedCompanions scan returns before reading (`:79`), so it saves
nothing today.

**Condition:** exclude by the path segment `Languages` directly under ModuleData, not by
`Contains("Languages")` on the full path (a future file named with that word elsewhere would vanish).
The lane's step "cache it with TST-01" is not needed and adds scope; drop it. The 66-file shared loader is
a separate refactor.

**Recalibrated:** impact about 1 to 3 s; effort S; risk LOW. Bundle with another test plan, not alone.

---

## TST-04: compile each shipped XSLT once. NARROWED

**Re-read:** every `new XslCompiledTransform()` site in TAOM.Tests (15 files). Loader style per site:

| Site | Stylesheet | Load call | Reader / XsltSettings / resolver |
|---|---|---|---|
| `Core/CommentStringTagsTests.cs:80-81` | `comment_strings.xslt` | `Load(path)` | defaults (A) |
| `Core/CultureExecutionerTests.cs:97-98` | `spcultures.xslt` | `Load(path)` | A |
| `Core/CulturePartyTemplateTests.cs:339-340` | `spcultures.xslt` | `Load(path)` | A, per call of `TransformSentinelStub` |
| `Core/LordTemplateRosterTests.cs:38-39` | `lord_template_rosters.xslt` | `Load(path)` | A |
| `Core/NotableTemplateGenderTests.cs:157-158` | `spcultures.xslt` | `Load(path)` | A |
| `Core/RenamedCultureWandererListTests.cs:33-34` | `spcultures.xslt` | `Load(path)` | A |
| `Features/CultureConversion/GarrisonCultureCoverageTests.cs:247-248` | `spcultures.xslt` | `Load(path)` | A |
| `Features/CustomBattles/CustomBattleLordStubsTests.cs:64-65` | `lords.xslt` | `Load(path)` | A |
| `Features/NazgulFamily/NazgulRaceDataTests.cs:45-46` | `lords.xslt` | `Load(path)` | A, per call |
| `Core/CustomBattleTroopFilterTests.cs:25-28` | `custom_battle/custom_battle_harad_troop_filter.xslt` | `Load(XmlReader, XsltSettings(false,false), null)` | B: `DtdProcessing.Ignore`, `XmlResolver = null` |
| `Core/LordFamilyTransformTests.cs:69-72` | parameter | same | B |
| `Core/VanillaWandererTemplateTransformTests.cs:37-40` | `lords.xslt` | same | B |
| `Features/Diplomacy/ShippedKingdomDataTests.cs:121-125` | parameter | same | B |
| `Features/Execution/ShippedClanSideCoverageTests.cs:116-119` | parameter | same | B |
| `Features/KeepWardens/ShippedCastleOwnerCultureTests.cs:36-39` | parameter | same | B |

Style A is `Load(string)`: the framework's default stylesheet reader and its default resolver for
`xsl:import`/`xsl:include`. Style B reads with DTDs ignored and no resolver. Both use document()
and script disabled. The input-side reader settings (`IgnoreWhitespace` at
`ShippedClanSideCoverageTests.cs:108` and `ShippedCastleOwnerCultureTests.cs:50`) apply to `Transform`,
not to `Load`, so they do not affect a shared compiled instance.

**Ran:** `git grep -nE 'xsl:import|xsl:include|document\(|<!DOCTYPE|msxsl:script|<!ENTITY' --
'Main/_Module/ModuleData/*.xslt'`: no hit in the 11 tracked stylesheets. So A and B compile the same
program for every shipped sheet today. The exact framework defaults of `Load(string)` I did not
decompile: UNVERIFIED.

**Corrections:** the lane's fix sketch uses style B for every site. That moves the nine style-A sites
onto another loader. It is safe today only because of the empty grep above; a later `xsl:include`
would resolve under A and fail under B. Compile counts are per test or per call, as the lane says
(`TransformSentinelStub` is called 8 times across the two files).

**Conditions:** key the cache on (full path, loader style) and copy each site's own `Load` call into the
helper, so no site changes loader; or convert A to B in a separate, named step with the grep above as
its evidence. Several B sites are `LiveInstall` (vanilla XML needed), so the saving is on the desktop.
Measure one `Load("lords.xslt")` first.

**Recalibrated:** impact UNVERIFIED (probably seconds, from about 20 compiles of 0.1 to 1.2 MB sheets);
effort S; risk LOW if keyed on loader style.

---

## TOOLS-01: delete five retired or superseded scripts. NARROWED

**Ran:** `git grep -n -w <stem> -- . ':!*.xml'` for each stem, with changelog and archive hits counted
apart (`tools01_refs.txt`).

**Split the finding in two:**

1. **`generate_xslt.py` and `generate_batch2_wanderers.py`: deletion is defensible.** The catalog says
   do not run either (`tools/README.md:173`, `:174`). Live references to edit in the same change:
   - `generate_xslt`: `tools/README.md:173`, `:712`; `docs/modding/file-catalogue.md:50`;
     `docs/reference/provenance-register.md:301` (provenance evidence: keep the name with "removed at
     <commit>", do not just drop it).
   - `generate_batch2_wanderers`: `tools/check_generator_item_refs.py:95`; `tools/README.md:41`
     ("both wanderer generators") and `:174`; `docs/modding/file-catalogue.md:65`, `:66`, `:67`;
     `docs/modding/validation-and-testing.md:206`; `docs/modding/wanderers-and-named-companions.md:259`;
     `docs/migration/v1.4.x-equipment-overhaul.md:166`, `docs/migration/v1.4.x-taom-impact.md:210`.
   - `tools/tests/test_check_generator_item_refs.py:131-145` iterates `GENERATORS` and asserts each path
     exists; it counts no entries, so dropping line 95 keeps it green. The "seven generators" in
     `tools/README.md:41`, `tools/validate_moduledata.py:100` and
     `docs/reference/moduledata-validator-codes.md:44` is dated 2026-09-13 history; leave it.
2. **`validate_all_troop_refs.py`, `validate_gondor_refs.py`, `audit_item_refs.py`: not a mechanical
   deletion.** The lane's "no code, test, hook or skill references it" is true for code, but these are
   live documented procedure steps:
   - `audit_item_refs.py --show-locations` is the "find consumers before deleting" step in
     `docs/modding/items-armor.md:285`, `:293`; `items-mounts-and-harness.md:464`; `items-shields.md:177`;
     `items-weapons-and-crafting.md:515`, `:521`; `module-armory.md:391`; `recipe-retire-content.md:154`;
     `validation-and-testing.md:35`, `:260-262`, `:287`. It prints file:line per broken ref, sorted by
     use count (`tools/audit_item_refs.py:118-126`).
   - `validate_all_troop_refs.py` is a check step in `docs/ai-includes/agent-operating-manual.md:45`,
     `docs/modding/troops.md:300`, `:341`, `:386`, `:420`, `recipe-add-a-kingdom.md:396`,
     `validation-and-testing.md:34`, `:253`, `:286`, and several feature docs. It was maintained on
     2026-09-01 (`sm_` prefix) and 2026-10-07 (folder read) per its own comments (`:33-38`, `:79-80`).
   - Equivalence: `validate_moduledata.py`'s item registry uses the same eight game modules plus TAOM
     (`tools/taom_schema.py:3050-3058` against `tools/audit_item_refs.py:26-36`). Two differences I can
     state: `validate_all_troop_refs.py` resolves armour-prefixed refs against the Armory only
     (`:43-47`, `:57-58`), stricter than a registry that also holds vanilla and TAOM items; and whether the
     schema-driven `BROKEN_ITEM_REF` covers every `id="Item.X"` attribute that the regex in
     `audit_item_refs.py:44` matches, and prints every location, is UNVERIFIED (I did not trace
     `taom_schema.py`'s 3,222 lines).
   - Lane facts wrong: `validate_all_troop_refs.py` does not import `validate_gondor_refs.py` (its
     imports are `re, os, sys, glob, _gamedir`, `:14-18`; line 5 names it only in the docstring).
     `validate_all_troop_refs.py:103` prints a hint to `audit_item_refs.py`, so deleting one breaks the
     other's printed advice.
   - **Only `validate_gondor_refs.py` fits a plain deletion**: its live mentions are
     `docs/features/gondor-armor-revamp.md` (6 lines), migration templates and one comment in
     `cleanup_deleted_gondor_items.py`'s neighbour (`tools/validate_gondor_refs.py:21`).

**Safety net correction:** `tools/lint_docs.py` checks only markdown links (`check_dead_links`,
`:362-385`, `LINK_RE` `:90`, inline code scrubbed first). Nearly every mention above is a backticked
path, which no gate checks. The executor must grep each stem after the change; `lint_docs.py` passing
proves nothing here. `tools/check_provenance.py` checks vocabulary, binaries and adoption records only
(`:4-10`), not tool paths.

**Recalibrated:** part 1 (two generators, plus `validate_gondor_refs.py`): impact low (about 1,060 lines
out of greps), effort S, risk LOW. Part 2 (`audit_item_refs.py`, `validate_all_troop_refs.py`): a
maintainer decision; deleting them changes about 25 live procedure lines and loses a per-location report.

---

## TOOLS-02: move 25 one-offs to `tools/oneoff/`. NARROWED (much smaller, not mechanical)

**Ran:** `tools02_refs.py` (every `git grep -w <stem>` hit outside `tools/README.md` and history folders,
plus each script's import and `__file__` lines); output in `tools02_refs.txt`. All 25 files exist.

**Corrections to the lane:**
- `translation_status.sh` is not a finished one-off. Its row says "One-shot status dashboard"
  (`tools/README.md:614`): a dashboard printed in one shot. It is a live step in
  `.claude/skills/localize/SKILL.md:44`, `docs/modding/strings-and-localization.md:196` and
  `tools/README.md:664`. Remove it from the list.
- The README's own move criterion is "referenced by no living doc" (`tools/README.md:309-313`: "The
  initial sweep moved 33 scripts referenced by no living doc"). Most of the 25 fail it. Live
  (non-history) references, file:line:
  - `apply_gondor_troop_revamp`: `docs/features/gondor-armor-revamp.md:29,47,97,116,125,158,176`;
    `docs/features/troop-tree-revamp.md:29,137`; `docs/migration/templates/troops-and-parties.md:723`;
    `docs/migration/v1.4.x-taom-impact.md:215`; `docs/modding/file-catalogue.md:93`; `tools/apply_gondor_polish_224.py:8`.
  - `apply_mordor_troop_revamp`: `troop-tree-revamp.md:119,162`; `file-catalogue.md:99`; `tools/apply_gondor_polish_224.py:9`.
  - `apply_isengard/dolguldur/gundabad/erebor_troop_revamp`: `troop-tree-revamp.md:120-123`; `file-catalogue.md:100,101,102,95`.
  - `apply_gondor_polish_224`: `troop-tree-revamp.md:186,199,248`; `file-catalogue.md:93`.
  - `apply_erebor_equipment_sweep`: `troop-tree-revamp.md:124,185`; `file-catalogue.md:95`.
  - `apply_umbar_equipment`: `file-catalogue.md:83,108,117,119`.
  - `cleanup_deleted_gondor_armor`: none.
  - `cleanup_deleted_gondor_items`: `tools/validate_gondor_refs.py:21` (comment); `tools/README.md:705` names its `TAOM_ARMORY_BASE` variable.
  - `cleanup_deleted_troops_212`: `troop-tree-revamp.md:40,64,125,153`; `file-catalogue.md:61,303,315`;
    `docs/modding/recipe-retire-content.md:114,117` (an `<!-- excerpt file=... -->` marker; no gate reads it,
    searched `tools/*.py`, `.claude`, `.githooks`), `:289`; `docs/modding/troops.md:357` (a live recipe pattern).
  - `expand_party_templates_212`: `troop-tree-revamp.md:41,67,126,154,163`; `file-catalogue.md:61`.
  - `rollback_erebor_iron_misfile`: `docs/features/multi-culture-armor-revamp.md:72,109`.
  - `add_education_roster_cultures`: `docs/migration/TRACKING.md:74`; `docs/migration/templates/equipment-rosters.md:398,566`; `file-catalogue.md:121`.
  - `apply_career_rank_names`: `file-catalogue.md:304`; `tools/career_rank_names.json:5` (data file naming its applier).
  - `fix_promoted_culture_flavor`: `docs/modding/recipe-add-a-culture.md:123,300` (a live `--apply` step,
    which contradicts its README row "no longer runs"); `file-catalogue.md:53,304`;
    `TAOM.Tests/Features/CharacterCreation/ClonedCultureFlavorTests.cs:202` (a C# failure message names the path).
  - `rename_maxcharge_to_cooldownreduction`: `docs/features/career-system.md:408`; `file-catalogue.md:305`.
  - `repaint_kingdom_colors`: `docs/modding/kingdoms.md:156` (markdown link), `:160` (live Check line);
    `docs/features/clan-heraldry.md:64`; `file-catalogue.md:51`.
  - `retune_phantom_passives`: `career-system.md:358`; `file-catalogue.md:305`; `tools/retune_phantom_descriptions.py:4`.
  - `retune_phantom_descriptions`: `career-system.md:72`; `file-catalogue.md:305`; `tools/retune_career_health.py:27`.
  - `generate_new_factions`: `docs/features/new-factions-misty-mountains-lindon.md:130,172,180` ("Re-run
    order"); `file-catalogue.md:83,102,117`; `validation-and-testing.md:207`; `tools/README.md:169`
    (`mordor_armor_remap.py` row says it is imported by this script).
  - `raise_party_template_maxes`: `docs/features/bandit-management.md:205`; `caravan-bandit-parity.md:62`;
    `file-catalogue.md:61`; `docs/reference/party-template-sizing.md:371`.
  - `patch_quad_movement`: `docs/ai-includes/creature-animation-blender-mcp-workflow.md:25,191` (markdown
    links), `:57`; `docs/features/chariot.md:32`.
- **Imports and path arithmetic break on a move one level down.** Every script computes the repo root
  from its own location: `dirname(dirname(abspath(__file__)))` (for example
  `apply_gondor_troop_revamp.py:32`, `cleanup_deleted_troops_212.py:19`, `expand_party_templates_212.py:22`,
  `apply_career_rank_names.py:22`, `generate_new_factions.py:27`), `Path(__file__).resolve().parent.parent`
  (`apply_umbar_equipment.py:73`, `add_education_roster_cultures.py:26`, `rename_maxcharge_to_cooldownreduction.py:31`,
  `raise_party_template_maxes.py:37`), `parents[1]` (`fix_promoted_culture_flavor.py:40`), or
  `dirname(__file__)/..` (`repaint_kingdom_colors.py:21`). After a move each resolves to `tools/` and
  writes or reads the wrong tree. Sibling imports also break: `from _gamedir import` in
  `apply_erebor_equipment_sweep.py:51`, `cleanup_deleted_gondor_armor.py:17`, `rollback_erebor_iron_misfile.py:20`,
  `patch_quad_movement.py:12`; `generate_new_factions.py:24-25` puts its own folder on `sys.path` and
  imports `mordor_armor_remap` (which stays in `tools/`). The existing folder shows the same hazard:
  `tools/oneoff/add_gundabad_clans.py:21` and `add_race_attribute.py:21` still use `parents[1]` after
  the 2026-07-12 move, so they point at `tools/` today (a lead, not checked further).
- **No suite proves a moved script still works.** None of the 25 has a test in `tools/tests`; several
  write on a plain run (`patch_quad_movement.py` "writes with no flag", README:446;
  `cleanup_deleted_gondor_items.py` takes no args, README row; the revamp scripts default to dry run).
  An executor cannot run them to check, so "same tool behaviour, proven by the existing suites" is not
  available for this finding.
- **Gate scope does not change with a move:** `ruff.toml` has no include or exclude and
  `tools/tests/test_python_lint_gate.py:4-5` runs over the whole repo; `lint_docs.check_rule_symbols`
  greps the pathspec `tools/*.py`, which in git matches subfolders too (`tools/lint_docs.py:1274-1275`);
  graphify indexes every tracked code file (`tools/graphify_taom.py:64`, `:188`); `check_generator_item_refs.py`
  lists none of the 25; `tools/oneoff/` has no convention file of its own (the convention is
  `tools/README.md:309-316`).

**What survives:** `cleanup_deleted_gondor_armor.py` (no live reference) and `cleanup_deleted_gondor_items.py`
(one comment) fit the README's own criterion. For those two, a move needs the root and `_gamedir`
import fixed (`sys.path.insert(0, <tools>)` as `tools/oneoff/author_dale_lords.py:60-63` does) and
the README rows moved. The six troop-revamp scripts sit under a heading that keeps them "for the
mechanical-swap pattern" (`tools/README.md:259`) and are named by two live recipes: a maintainer decision.

**Recalibrated:** impact low (grep noise; the 9,264 lines are not run by anything); effort M to L for
all 25 (about 90 live doc lines plus per-script path fixes, no test proof); risk MED (a moved writer
with a wrong root writes into `tools/`). Plan-ready only for the two Gondor cleanup scripts.

---

## TOOLS-03: three slow whole-tree tests. NARROWED (one third refuted, one third holds)

**Re-read:** `tools/tests/test_ingame_verdict.py:239-337`, `tools/tests/test_doc_graph_ratchet.py` in full,
`tools/check_doc_graph_ratchet.py:44-67`.

**Ran:**
- `ingame_prefilter.py` (imports the test module's own `CS_TOKEN`, `cs_string_literals` and
  `taom_source_files`): 22 manifests, 110 wanted ids, 4,622 files; enumerate 0.17 s, read 0.31 s,
  **lex all 0.12 s**, prefilter plus lex **0.69 s** (110 substring scans per file cost more than the
  lexer), 30 files lexed; same verdict for every id.
- `time_tests.py`: `test_every_named_check_id_appears_in_a_taom_source_file` **0.71 s** and 0.72 s
  (two trials); `python -B -m unittest tools.tests.test_ingame_verdict.RepoManifestGateTests`:
  "Ran 8 tests in 1.674s", OK. `tools.tests.test_doc_graph_ratchet`: 15 tests in 9.37 s and 9.69 s.

**Corrections:**
- The 17.1 s the lane measured for the ingame test does not reproduce warm: it was cold file reads
  or contention. The lexer is not the cost, and the proposed prefilter is slower than what ships.
  The prefilter's logic is sound (the lexer does not unescape, `test_ingame_verdict.py:246-255`, so a
  literal equal to an id is a substring of the raw text), but it saves nothing. REFUTED for this part.
- `git ls-files Main Client CoopCompat -- '*.cs'` lists every file in the three folders plus every
  `.cs` in the repo (a pathspec union), so its 10,154 is not the file count; the gate reads 4,622.
- Doc graph part holds: `test_orphan_docs_agrees_with_the_orphan_count` (`:114`) builds the graph twice
  (`orphan_docs()` and `current_metrics()` each call `_orphans_and_components()`, ratchet `:51-66`), and
  `LiveTreeTests` (`:121-126`) builds it once more: three builds, about 3 s each.

**Conditions (graph part):** share one `_orphans_and_components()` result in the test module, for
example by `mock.patch.object(ratchet, "_orphans_and_components", <a cached copy>)` inside those two
tests, or a module-level memo in the tool. `FailureTextTests` patches `current_metrics` and
`orphan_docs`, not `_orphans_and_components` (`:91-97`), and no other tool or test patches the
ratchet's `DOCS_DIR` (searched `tools/*.py`, `.githooks`, `.claude/hooks`, `.claude/skills`), so either
form keeps those tests' meaning. With one shared result, the agreement test checks only the two
projections; they already derive from one function, so no real coverage is lost.

**Recalibrated:** impact about 6 s per tools-suite run (doc graph only); effort S; risk LOW.

---

## TOOLS-04: hoist the sibling-file parse out of `skip_ids_for`. CONFIRMED with one condition

**Re-read:** `tools/translate_with_claude.py:343-363` (`skip_ids_for`), `:253-267` (`discover_entries`),
`:1016-1027` (`--sync-ids`), `:802-831` (`sync_missing_ids`, which writes `target`);
`tools/tests/test_translate_batching.py:448-467`. Callers: production `:266` and `:1022`; tests
`test_translate_batching.py:330,354,408,417,437,444,463`.

**Ran:** `time_tests.py`: `ShippedKeyCoverageTests` 24.39 s and 24.53 s. `skipids_hoist.py`: for all
12 languages times 23 sources, per-call `skip_ids_for` 23.80 s; a per-language precompute of each
`std_taom_*.xml` file's ids, then the same union and subtraction, 6.65 s; **the 276 result sets are
equal**. The remaining 6.65 s is `_parse_string_xml(target)` plus `override_ids_to_skip` (which parses
`target` again), per call.

**Staleness trace:**
- `discover_entries` (`:262-267`): one language per run, `skip_ids_for` then `_diff_files`, which only
  reads. No write between calls: a per-language precompute is exact.
- `--sync-ids` (`:1019-1026`): each iteration calls `skip_ids_for`, then `sync_missing_ids` writes
  `target` when not a dry run. The next iteration reads that file as a sibling. A set computed before the
  loop would miss the ids just seeded. Today those ids are also in `elsewhere` through `owners` (a seeded
  id is a key of the source whose target was written, and `owners` maps it to that source), and the 23
  sources map to 23 distinct target templates (`_loc_sources.TAOM_SOURCES`: 23 entries, 23 unique), so
  the result would probably still match; that argument is mine and UNVERIFIED by a test. The safe form:
  hoist in `discover_entries` and the test only, or recompute the written file's ids after each write.
- The "a sibling that does not parse raises" contract (`:353-354`, `:358-361`) holds if the precompute
  raises the same `SystemExit` before the first diff; nothing is printed or written in between in
  `discover_entries`.

**Corrections:** the real translator saving is small (one language per run, so 23 times about 23
parses, about 2 s); the lane's "every translator run" gain is mostly a test gain. The test-side hoist of
`english_only_override_ids(src)` and `_parse_string_xml(src)` out of the language loop
(`:462`, `:464`) is pure test code and safe.

**Recalibrated:** impact about 17 s per tools-suite run; effort S; risk LOW with the `--sync-ids`
condition.

---

## TOOLS-08: stale counts in the catalog's environment section. CONFIRMED (one item narrowed)

**Ran** (non-test files under `tools/`):
- `git grep -l "BANNERLORD_GAME_DIR" -- 'tools/*.py' ':!tools/tests' | wc -l`: **51** (46 top-level).
- `git grep -lE "(from|import) +_gamedir|from tools\._gamedir|import tools\._gamedir" -- 'tools/*.py' ':!tools/tests' | wc -l`: **91** (86 top-level).
- Union of the two: **105**; files that mention the variable and do not import `_gamedir`: **14**.
- `git grep -n -i -E 'Users.{1,3}mikew' -- 'tools/*.py'`: `tools/generate_xslt.py:295` and
  `tools/oneoff/lords-migration/replace_equipment_templates.py:12` write or read the repo through that
  path; `tools/blender/rebuild_anim_from_json.py` has none (it exists, and greps clean for `mikew` and
  `Users`). The other hits are usage docstrings in `tools/blender/` and `tools/painter/painter_run.py:4`.
- `package_quad_toolkit.py`: no README row; named only in `LICENSE-CONTENT.md:51`, `tools/quad_toolkit/NOTICE.txt:13`
  and its own file list (`:22`).

All three counts in `tools/README.md:685-686` ("29 tools honour it: 25 resolve it themselves") and
`:692` ("22 of the 25 do") are stale against these numbers; the README does not define "resolve it
themselves" precisely enough to pick one replacement number, so the lane's fix (name the commands, not
the numbers) is right. Narrowed: `:707` ("The thirteen top-level tools that had no override ... now all
read it (#404)") is a dated record of one change, not a live count; leave it or date it. The
`rebuild_anim_from_json.py` path item at `:712` is wrong and should go with the `generate_xslt.py` item
when TOOLS-01 part 1 lands.

**Recalibrated:** impact low (doc accuracy); effort S; risk LOW (doc only).

---

## What I did not cover

- No dotnet run, so no .NET timing for TST-01, TST-02 or TST-04; their numbers are Python floors or
  UNVERIFIED, and each block names the measurement a plan must take first.
- I did not decompile `XslCompiledTransform.Load(string)` to state its exact reader and resolver
  defaults on net472; the A/B equivalence rests on the empty import/DTD grep over the 11 shipped sheets.
- I did not trace `taom_schema.py` to prove that `BROKEN_ITEM_REF` reports every `id="Item.X"` site
  `audit_item_refs.py` matches, with locations; TOOLS-01 part 2 stays UNVERIFIED on equivalence.
- I did not run any of the 25 TOOLS-02 scripts (several write on a plain run) and did not check whether
  the two `tools/oneoff/` scripts with `parents[1]` are broken in use.
- The `--sync-ids` staleness argument in TOOLS-04 is reasoned from the code, not tested.
- Findings outside my list (TST-05 to TST-08, TOOLS-05 to TOOLS-07, TOOLS-09, TOOLS-10) were not
  re-verified.
- I made no `gh` call, so I cannot say whether issues already cover any of these.
